using BWJ.Core;
using RaspberryBeret.DataBinding;
using RaspberryBeret.Elements;
using RaspberryBeret.Parsing;
using RaspberryBeret.ReferenceData;
using RaspberryBeret.Styling;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Compilation;
internal static class CompileUtils
{
    /// <summary>
    /// Converts a PDFML text document into a DOM, including as many externally
    /// referenced PDFML templates as possible without performing data binding.
    /// </summary>
    /// <param name="pdfml">PDFML document text to compile</param>
    /// <param name="rootPath">The root path used to resolve relative path references.
    /// Typically the directory where the PDFML document file is located.</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>PDF document object model</returns>
    public static Element PrecompilePDFML(
        string pdfml,
        string? rootPath = null,
        ICloudResourceService? cloudService = null)
    {
        //prepare document for parsing
        var doc = ParseUtils.FlattenTemplate(pdfml);
        doc = ParseUtils.RemoveComments(doc);
        doc = ParseUtils.NormalizeWhitespace(doc);

        // The DOM is the <pdfml> element -- we don't care about any other root level element
        // (any other root-level element, or any nth pdfml element, is illegal)
        var dom = ParseUtils.CreateDOM(doc).FirstOrDefault(d => d.Tag.Name == TagName.pdfml);
        if (dom == null)
        {
            throw new ParsingException("Document is missing root element 'pdfml'", pdfml, 0);
        }

        dom.RootPath = rootPath;
        ResolveIncludesForTemplatePrecompilation(dom, cloudService);
        ResolveStylesForTemplatePrecompilation(dom, cloudService);

        return dom;
    }

    /// <summary>
    /// Fully compiles a precompiled DOM, resolving all special elements and data binding
    /// references
    /// </summary>
    /// <param name="dom">Precompiled DOM to fully compile</param>
    /// <param name="model">Data model used to resolve binding references</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    public static void CompileDOM(Element dom, BindingModel model, ICloudResourceService? cloudService = null)
    {
        //apply binding model for entire DOM
        BindUtils.ApplyBindingModel(model, dom);

        //perform data binding and resolve binding elements
        bool bindingIncomplete = false;
        for (int i = 0; i < 128; i++)
        {
            bindingIncomplete = PerformCompilationPass(dom, cloudService);
            if (!bindingIncomplete) { break; }
        }
        //binding must be completed in 128 passes or less
        if (bindingIncomplete)
        {
            ParseUtils.ThrowParsingException(dom,
                "Unable to compile PDFML document - too many nested binding references encountered.");
        }

        //resolve image source paths
        var images = dom.AllDescendants.Where(e => e.Tag.Name == "img");
        foreach (var img in images)
        {
            var src = img.GetAttributeValue("src");
            if (!string.IsNullOrWhiteSpace(src))
            {
                if (DataUtils.IsSourceCloudStorage(src))
                {
                    img.Tag.Attributes["src"] =
                        DataUtils.GetBase64StringFromBlob(src, cloudService, img) ?? string.Empty;
                }
                else if(DataUtils.IsSourceWebBased(src))
                {
                    img.Tag.Attributes["src"] =
                        DataUtils.GetBase64StringFromWeb(src) ?? string.Empty;
                }
                //resource on disk
                else if (DataUtils.IsSourceBase64(src) == false)
                {
                    img.Tag.Attributes["src"] =
                        ParseUtils.ResolveSourcePath(img.GetRootPath(), src) ?? string.Empty;
                }
            }
        }

        CullUnusedElements(dom);

        //ensure that DOM structure is valid
        dom.Metadata.ValidateElement(dom);
        foreach (var element in dom.AllDescendants)
        {
            element.Metadata.ValidateElement(element);
        }

        //apply styling
        StyleUtils.ApplyStylesToDOM(dom);

        //enclose inline text elements that are direct children of table cells
        //in a paragraph element
        WrapInlineText(dom);

        //determine widths for table, paragraph elements
        var contentsElement = dom.Children.FirstOrDefault(e => e.Tag.Name == TagName.contents);
        SizeContentsChildren(contentsElement);
    }

    /// <summary>
    /// Enclose inline text elements that are direct children of table cells
    /// in a paragraph element
    /// </summary>
    /// <param name="dom">Document Object Model to work on</param>
    private static void WrapInlineText(Element dom)
    {
        var cells = dom.AllDescendants
            .Where(c => c.Metadata.GroupMembership.Contains(ElementGroup.TableOrGridCell));
        foreach (var cell in cells)
        {
            Element? currentParagraph = null;
            foreach (var element in cell.Children)
            {
                if (element.Metadata.GroupMembership.Contains(ElementGroup.InlineText))
                {
                    //add to current wrapper paragraph, or create a new one
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Element(new Tag("p", TagType.Opening));
                        element.Parent!.AddChildBefore(currentParagraph, element);
                    }

                    element.Parent!.RemoveChild(element);
                    currentParagraph.AddChild(element);
                }
                else
                {
                    currentParagraph = null;
                }
            }
        }
    }

    /// <summary>
    /// Removes all unused/unsuppported elements from the given DOM
    /// </summary>
    /// <param name="dom">Document Object Model to work on</param>
    private static void CullUnusedElements(Element dom)
    {
        bool cullingComplete = false;
        while (!cullingComplete)
        {
            cullingComplete = true;

            foreach (var element in dom.AllDescendants)
            {
                //elements with no metadata are unsupported,
                //or use metadata to determine if element should be deleted
                if (element.Metadata == null ||
                    element.Metadata.ShouldDiscardElement(element))
                {
                    element.Parent!.RemoveChild(element);
                    cullingComplete = false;
                }
            }
        }
    }

    /// <summary>
    /// Makes a single pass through all the elements in the DOM, resolving any special
    /// elements or binding references encountered
    /// </summary>
    /// <param name="dom">DOM to do work on</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>True if DOM was modified at all, false if not</returns>
    private static bool PerformCompilationPass(Element dom, ICloudResourceService? cloudService = null)
    {
        bool somethingChanged = false;

        //resolve attributes on root element
        if (!dom.Tag._Compiled && CompileElementAttributes(dom))
        {
            somethingChanged = true;
        }

        foreach (var element in dom.AllDescendants)
        {
            //resolve attributes
            if (!element.Tag._Compiled && CompileElementAttributes(element))
            {
                somethingChanged = true;
            }
            else if (!element._Compiled)
            {
                if (element.Tag.Name == "textrun")
                {
                    if (CompileTextrun(element)) { somethingChanged = true; }
                    else { element._Compiled = true; }
                }
                else if (element.Tag.Name == "style")
                {
                    if (CompileStyle(element, cloudService)) { somethingChanged = true; }
                    element._Compiled = true; //style tags are only evaluated once
                }
                else if (element.Metadata != null &&
                    element.Metadata.GroupMembership.Contains(ElementGroup.Virtual))
                {
                    CompileVirtual(element, cloudService);
                    somethingChanged = true;
                }
                else
                {
                    element._Compiled = true;
                }
            }
        }

        return somethingChanged;
    }

    /// <summary>
    /// Resolves binding references encountered in a run of text
    /// </summary>
    /// <param name="textrun">Textrun element</param>
    /// <returns>True if any binding references were resolved</returns>
    private static bool CompileTextrun(Element textrun)
    {
        var originalValue = textrun.InnerText;
        var boundValue = BindUtils.BindTextString(originalValue, textrun.DataModel);
        bool somethingChanged = originalValue != boundValue;

        if (somethingChanged)
        {
            //compile the bound text into element objects and add to the DOM
            var nuElements = ParseUtils.CreateDOM(boundValue);
            foreach (var ne in nuElements)
            {
                BindUtils.ApplyBindingModel(textrun.DataModel, ne);
                textrun.Parent!.AddChildBefore(ne, textrun);
            }

            //remove old textrun
            textrun.Parent!.RemoveChild(textrun);
        }

        return somethingChanged;
    }

    /// <summary>
    /// Converts a virtual element into the DOM elements it generates
    /// </summary>
    /// <param name="element">Virtual element</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    private static void CompileVirtual(Element element, ICloudResourceService? cloudService = null)
    {
        //make sure element is valid
        element.Metadata.ValidateElement(element);

        // include, show, hide, context, foreach
        switch (element.Tag.Name)
        {
            case "include":
                CompileInclude(element, cloudService);
                break;
            case "show":
            case "hide":
                CompileShowOrHide(element);
                break;
            case "context":
                CompileContext(element);
                break;
            case "foreach":
                CompileForeach(element);
                break;
        }
    }

    /// <summary>
    /// Resolves an include element, modifying the DOM
    /// </summary>
    /// <param name="include">Include element</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    private static void CompileInclude(Element include, ICloudResourceService? cloudService = null)
    {
        try
        {
            //get elements to include
            var includedElements = PrecompileInclude(include, cloudService);
            //apply context to those elements
            foreach (var ie in includedElements)
            {
                BindUtils.ApplyBindingModel(include.DataModel, ie);
                include.Parent!.AddChildBefore(ie, include);
            }

            //remove include element from DOM
            include.Parent!.RemoveChild(include);
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(include, e.Message, e);
        }
    }

    /// <summary>
    /// Resolves a style element, modifying the DOM if it contains a valid external
    /// stylesheet reference
    /// </summary>
    /// <param name="style">Style element</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    private static bool CompileStyle(Element style, ICloudResourceService? cloudService)
    {
        try
        {
            //get stylesheet contents
            var stylesheet = PrecompileStyle(style, cloudService);
            if (stylesheet == null) { return false; }//no DOM change made

            //apply context to contents, and add to style
            BindUtils.ApplyBindingModel(style.DataModel, stylesheet);
            style.AddChild(stylesheet);
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(style, e.Message, e);
        }

        return true; //if we get here, a DOM change occurred
    }

    /// <summary>
    /// Resolves a show or hide element, modifying the DOM
    /// </summary>
    /// <param name="element">Show or Hide element</param>
    private static void CompileShowOrHide(Element element)
    {
        var parent = element.Parent!;
        try
        {
            bool targetValue, actualValue;
            var predicate = string.Empty;

            if (!string.IsNullOrWhiteSpace(element.GetAttributeValue("if")))
            {
                predicate = element.GetAttributeValue("if")!;
                targetValue = true;
            }
            else
            {
                predicate = element.GetAttributeValue("ifnot")!;
                targetValue = false;
            }

            //resolve predicate
            var predicateVal = ResolveVisibilityPredicate(predicate, element.DataModel);
            actualValue = BindUtils.ResolveTruthiness(predicateVal);

            bool result = targetValue == actualValue;
            bool isShowElement = element.Tag.Name == "show";
            //if it is true that the given element is 'show', and the result of analyzing
            //the predicate is true, or if the given element is 'hide, and the result
            //is false, compile the element
            bool compile = isShowElement == result;

            if (compile)
            {
                //this element's only child is a text run, which we'll compile into
                //element objects and add to the DOM
                var nuElements = ParseUtils.CreateDOM(element.Children[0].InnerText);
                foreach (var ne in nuElements)
                {
                    BindUtils.ApplyBindingModel(element.DataModel, ne);
                    parent.AddChildBefore(ne, element);
                }
            }

            parent.RemoveChild(element);
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(element, e.Message, e);
        }
    }

    private static object? ResolveVisibilityPredicate(string predicate, BindingModel model)
    {
        if(BindUtils.ContainsBindingExpression(predicate))
        {
            //strip curly braces from binding expression
            predicate = Regex.Replace(predicate, @"(^\{\{\s*)|(\s*\}\}$)", string.Empty, RegexOptions.IgnoreCase);
            //get format portion of binding expression, if present
            string fmtPart = @"\|\s*([^\s`]*|`[^`]*`)$";
            Match fmtMatch = Regex.Match(predicate, fmtPart);
            if (fmtMatch.Success)
            {
                predicate = predicate.Remove(fmtMatch.Index).Trim();
            }
        }

        return BindUtils.ResolveModelReference(predicate, model);
    }

    /// <summary>
    /// Resolves a context element, modifying the DOM
    /// </summary>
    /// <param name="context">Context element</param>
    private static void CompileContext(Element context)
    {
        var parent = context.Parent!;
        try
        {
            //this element's only child is a text run, which we'll compile into
            //element objects and add to the DOM
            var nuElements = ParseUtils.CreateDOM(context.Children[0].InnerText);
            foreach (var ne in nuElements)
            {
                BindUtils.ApplyBindingModel(context.DataModel, ne);
                parent.AddChildBefore(ne, context);
            }

            parent.RemoveChild(context);
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(context, e.Message, e);
        }
    }

    /// <summary>
    /// Resolves a foreach element, modifying the DOM
    /// </summary>
    /// <param name="frch">Foreach element</param>
    private static void CompileForeach(Element frch)
    {
        var parent = frch.Parent!;
        try
        {
            //get the number of members in the context enumerable
            int memberCount = BindUtils.GetEnumerableCount(frch.DataModel.CurrentContext);
            if (memberCount > 0)
            {
                //use this element's context declaration to create a template to 
                //generate context declarations for each iteration
                var contextDeclaration = frch.GetAttributeValue("context") ?? string.Empty;
                BindUtils.ValidateContextDeclaration(contextDeclaration);
                contextDeclaration = Regex.Replace(contextDeclaration, @"\s+", " ");
                var cdParts = contextDeclaration.Split(' ');
                var modelRef = cdParts[0].Trim();
                var contextName = cdParts[2].Trim();// index 1 is 'as'
                var template = modelRef + "[{0}] as " + contextName;

                //foreach's data model's current context is the list we are
                //iterating over - we need that info, but its predecessor is the
                //real current context
                var parentModel = frch.DataModel;
                var sourceCollection = parentModel.CurrentContext;
                parentModel.ContextTree.Remove(parentModel.NameOfCurrentContext);
                var ccKeyIndex = parentModel.ContextTree.Count - 1;
                var ccKey = parentModel.ContextTree.Keys.ToArray()[ccKeyIndex];
                parentModel.CurrentContext = parentModel.ContextTree[ccKey];

                //generate a set of DOM elements for each of the source collection's items
                for (int i = 0; i < memberCount; i++)
                {
                    //create a binding model with a current context that is the 
                    //current index item
                    var cd = string.Format(template, i);
                    var model = BindUtils.CreateBindingModel(cd, parentModel);
                    model.SourceCollection = sourceCollection;
                    model.Index = i;

                    //this element's only child is a text run, which we'll compile into
                    //element objects and add to the DOM
                    var nuElements = ParseUtils.CreateDOM(frch.Children[0].InnerText);
                    foreach (var ne in nuElements)
                    {
                        BindUtils.ApplyBindingModel(model, ne);
                        parent.AddChildBefore(ne, frch);
                    }
                }
            }

            parent.RemoveChild(frch);
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(frch, e.Message, e);
        }
    }

    /// <summary>
    /// Resolves all binding references found in an element's attribute values
    /// </summary>
    /// <param name="element">The element to act on</param>
    /// <returns>True if any of the element's attributes were modified</returns>
    private static bool CompileElementAttributes(Element element)
    {
        bool somethingChanged = false;
        var attrs = element.Tag.Attributes;
        //make a separate list of attributes for enumeration, because enumerating on the
        //attribute dictionary directly will cause an exception to be thrown if any attribute value
        //is changed
        var enumList = attrs.ToList();

        foreach (var attr in enumList)
        {
            var boundValue = BindUtils.BindTextString(attr.Value, element.DataModel);
            if (attr.Value != boundValue)
            {
                attrs[attr.Key] = boundValue;
                somethingChanged = true;
            }
        }

        //if nothing changed, the element's attributes are compiled
        if (!somethingChanged) { element.Tag._Compiled = true; }

        return somethingChanged;
    }

    /// <summary>
    /// Resolves as many include statements as possible during template precompilation
    /// </summary>
    /// <param name="dom">DOM containing include elements to resolve</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>A list of the file paths of all externally referenced
    /// PDFML documents that were included in this precompilation</returns>
    private static List<string> ResolveIncludesForTemplatePrecompilation(Element dom,
        ICloudResourceService? cloudService)
    {
        HashSet<string> resolvedIncludePaths = new HashSet<string>();

        //resolve no deeper than 128 levels
        for (int cnt = 0; cnt < 128; cnt++)
        {
            bool resolutionComplete = true;
            //resolve includes that don't require data binding
            var includes = dom.AllDescendants.Where(i => i.Tag.Name == "include" &&
                Stringy.Relevant(i.GetAttributeValue("src")) &&
                BindUtils.ContainsBindingExpression(i.GetAttributeValue("src")!) == false);

            foreach (var inc in includes)
            {
                var includeContext = inc.GetAttributeValue("context");
                var includeDOM = PrecompileInclude(inc, cloudService);

                if (includeDOM.Count > 0)
                {
                    resolvedIncludePaths.Add(inc.GetAttributeValue("src")!);

                    foreach (var id in includeDOM)
                    {
                        //if this include specifies a context, and the element
                        //it created does not, add the context on the new element
                        if (!string.IsNullOrWhiteSpace(includeContext) &&
                            string.IsNullOrWhiteSpace(id.GetAttributeValue("context")))
                        {
                            id.Tag.Attributes["context"] = includeContext;
                        }
                        inc.Parent!.AddChildBefore(id, inc);
                    }
                    resolutionComplete = false;
                }

                inc.Parent!.RemoveChild(inc);
            }

            if (resolutionComplete) { break; }
        }

        return resolvedIncludePaths.ToList();
    }

    /// <summary>
    /// Resolves as many external stylesheet references as possible during template precompilation
    /// </summary>
    /// <param name="dom">DOM containing style elements to resolve</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>A list of the file paths of all externally referenced
    /// stylesheets that were included in this precompilation</returns>
    private static List<string> ResolveStylesForTemplatePrecompilation(Element dom,
        ICloudResourceService? cloudService)
    {
        HashSet<string> resolvedStylesheetPaths = new HashSet<string>();

        //resolve stylesheet references that don't require data binding
        var styles = dom.AllDescendants.Where(s => s.Tag.Name == "style" &&
            !string.IsNullOrWhiteSpace(s.GetAttributeValue("src")) &&
            BindUtils.ContainsBindingExpression(s.GetAttributeValue("src")!) == false);

        foreach (var syl in styles)
        {
            var stylesheetText = PrecompileStyle(syl, cloudService);
            if (stylesheetText != null)
            {
                resolvedStylesheetPaths.Add(syl.GetAttributeValue("src")!);
                syl.AddChild(stylesheetText);
                //remove the src attribute so it doesn't get resolved again during compilation
                syl.Tag.Attributes.Remove("src");
            }
        }

        return resolvedStylesheetPaths.ToList();
    }

    /// <summary>
    /// Resolves an include element by obtaining its referenced content, and converts
    /// the included PDFML document snippet into a DOM
    /// </summary>
    /// <param name="include">Include element to work with</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>A DOM of all root-level elements found in the include file,
    /// or an empty list if the include element could not be resolved</returns>
    private static List<Element> PrecompileInclude(Element include, ICloudResourceService? cloudService)
    {
        //src attribute must be present
        var src = include.GetAttributeValue("src");
        if (string.IsNullOrWhiteSpace(src)) { return new List<Element>(); }

        //attempt to load the PDFML snippet to insert
        string? snippet = null;
        try
        {
            if (DataUtils.IsSourceCloudStorage(src))
            {
                snippet = DataUtils.GetTextFromBlob(src, cloudService);
            }
            else if(DataUtils.IsSourceWebBased(src))
            {
                snippet = DataUtils.GetTextFromWeb(src);
            }
            else
            {
                src = ParseUtils.ResolveSourcePath(include.GetRootPath(), src);
                if (src == null) { return new List<Element>(); }

                using (StreamReader sr = new StreamReader(src))
                {
                    snippet = sr.ReadToEnd();
                }
            }
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(include,
                "An exception occurred while attempting to resolve include.", e);
        }

        //return the compiled snippet
        snippet = ParseUtils.FlattenTemplate(snippet);
        snippet = ParseUtils.RemoveComments(snippet);
        snippet = ParseUtils.NormalizeWhitespace(snippet);
        var elements = ParseUtils.CreateDOM(snippet);

        //set the root path for the elements -- elements from an external source have no root path
        src ??= string.Empty; // compiler appeasement
        if (DataUtils.IsSourceCloudStorage(src) == false && DataUtils.IsSourceWebBased(src) == false)
        {
            string rootPath = src;
            foreach (var elem in elements)
            {
                elem.RootPath = rootPath;
            }
        }

        return elements;
    }

    /// <summary>
    /// Resolves an style element by converting any external source it references
    /// into a textrun element
    /// </summary>
    /// <param name="style">Include element to work with</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <returns>A textrun element referencing any external CSS, or null if no such 
    /// valid reference exists</returns>
    private static Element? PrecompileStyle(Element style, ICloudResourceService? cloudService)
    {
        //nothing to do if src attribute not present
        var src = style.GetAttributeValue("src");
        if (string.IsNullOrWhiteSpace(src)) { return null; }

        //attempt to load the CSS to insert
        string? css = null;
        try
        {
            if (DataUtils.IsSourceCloudStorage(src))
            {
                css = DataUtils.GetTextFromBlob(src, cloudService);
            }
            else if(DataUtils.IsSourceWebBased(src))
            {
                css = DataUtils.GetTextFromWeb(src);
            }
            else
            {
                src = ParseUtils.ResolveSourcePath(style.GetRootPath(), src);
                if (src == null) { return null; }

                using (StreamReader sr = new StreamReader(src))
                {
                    css = sr.ReadToEnd();
                }
            }
        }
        catch (Exception e)
        {
            ParseUtils.ThrowParsingException(style,
                "An exception occurred while attempting to resolve style.", e);
        }

        //format and return the CSS
        css = ParseUtils.FlattenTemplate(css);
        css = ParseUtils.RemoveComments(css);
        css = ParseUtils.NormalizeWhitespace(css);
        css = ParseUtils.ResolveEncodedChars(css);

        //handle an empty external reference
        if (string.IsNullOrWhiteSpace(css)) { return null; }

        Element trun = new Element(new Tag("textrun", TagType.Opening));
        trun.InnerText = css;

        return trun;
    }

    private static double GetDefaultElementWidth(Element e, double parentWidth)
    {
        double margins = 0;
        //get total lateral margin width
        var lmStyle = e.Styles.FirstOrDefault(s => s.Name == "margin-left");
        if (lmStyle != null)
        {
            var lmValue = (NumericStyleValue)lmStyle.Value;
            margins += lmValue.GetValueInInches(parentWidth);
        }
        var rmStyle = e.Styles.FirstOrDefault(s => s.Name == "margin-right");
        if (rmStyle != null)
        {
            var rmValue = (NumericStyleValue)rmStyle.Value;
            margins += rmValue.GetValueInInches(parentWidth);
        }


        //default width is always the element's parent's width, less lateral margins
        var defWidth = parentWidth - margins;
        //no element can be less than minimum size
        if (defWidth < MINIMUM_ELEMENT_WIDTH) { defWidth = MINIMUM_ELEMENT_WIDTH; }

        return defWidth;
    }

    private static NumericStyleValue? EnsureAcceptablePageMargin(Style? marginStyle, double pageDimension)
    {
        if (marginStyle != null)
        {
            var valObj = (NumericStyleValue)marginStyle.Value;
            var inches = valObj.GetValueInInches();
            if (inches < MINIMUM_PAGE_MARGIN)
            {
                valObj.Value = MINIMUM_PAGE_MARGIN;
                valObj.Units = StyleUnit.Inch;
            }

            var maxMargin = pageDimension * 0.45d;
            if(inches >  maxMargin)
            {
                valObj.Value = maxMargin;
                valObj.Units = StyleUnit.Inch;
            }

            return valObj;
        }
        else { return null; }
    }

    /// <summary>
    /// Sets the layout widths of children of the contents element,
    /// and ensures page margin are within tolerance
    /// </summary>
    /// <param name="contents">Contents element</param>
    private static void SizeContentsChildren(Element contents)
    {
        //size documents
        foreach (var document in contents.Children)
        {
            //only currently supported page size is 8.5" X 11" (LETTER)
            double pageWidth = 8.5d;
            double pageHeight = 11.0d;
            if (document.GetAttributeValue("orientation") == "landscape")
            {
                pageWidth = 11.0d;
                pageHeight = 8.5d;
            }

            //smallest margin allowed is 1/4"
            var margin = document.Styles.FirstOrDefault(s => s.Name == "margin-top");
            EnsureAcceptablePageMargin(margin, pageHeight);

            margin = document.Styles.FirstOrDefault(s => s.Name == "margin-left");
            var marginLeftVal = EnsureAcceptablePageMargin(margin, pageWidth);

            margin = document.Styles.FirstOrDefault(s => s.Name == "margin-bottom");
            EnsureAcceptablePageMargin(margin, pageHeight);

            margin = document.Styles.FirstOrDefault(s => s.Name == "margin-right");
            var marginRightVal = EnsureAcceptablePageMargin(margin, pageWidth);

            //set element width
            document.Width = pageWidth - (marginLeftVal.Value + marginRightVal.Value);
            //size this element's children
            SizeDocumentChildren(document);
        }
    }

    /// <summary>
    /// Sets the explicitly defined width of the given element, if defined
    /// </summary>
    /// <param name="e">Element to set explicit width of</param>
    private static void SetExplicitElementWidth(Element e)
    {
        var widthStyle = e.Styles.FirstOrDefault(s => s.Name == "width");
        if (widthStyle != null)
        {
            var val = (NumericStyleValue)widthStyle.Value;
            if(e.Parent is null)
            {
                throw new Exception("Cannot assign a width to the root element");
            }
            e.Width = val.GetValueInInches(e.Parent.Width);
            e.IsWidthExplicit = true;
        }
    }

    /// <summary>
    /// Sets the layout widths of children of the PDFML document element
    /// </summary>
    /// <param name="document">Document element</param>
    private static void SizeDocumentChildren(Element document)
    {
        //size documents
        foreach (var element in document.Children)
        {
            element.Width = GetDefaultElementWidth(element, document.Width);

            //handle grids or tables, which can have a custom width
            if (element.Tag.Name == "grid")
            {
                SetExplicitElementWidth(element);

                SizeGridChildren(element);
            }
            else if (element.Tag.Name == "table")
            {
                SetExplicitElementWidth(element);

                SizeTableChildren(element);
            }
            //header or footer element
            else
            {
                SizeHeaderFooterChildren(element);
            }
        }
    }

    /// <summary>
    /// Sets the layout widths of children of a header or footer element
    /// </summary>
    /// <param name="headfoot">Header or footer element</param>
    private static void SizeHeaderFooterChildren(Element headfoot)
    {
        //size grids/tables
        foreach (var grid in headfoot.Children)
        {
            //by default, grids and tables are the same width as their parent
            grid.Width = headfoot.Width;
            SetExplicitElementWidth(grid);

            SizeGridChildren(grid);
        }
    }

    /// <summary>
    /// Sets the layout widths of children of the grid element
    /// </summary>
    /// <param name="grid">Grid element</param>
    private static void SizeGridChildren(Element grid)
    {
        //size rows
        foreach (var row in grid.Children)
        {
            //all rows are the same width as their parent
            row.Width = grid.Width;

            SizeRowChildren(row);
        }
    }

    /// <summary>
    /// Sets the layout widths of children of the grid row element
    /// </summary>
    /// <param name="row">Row element</param>
    private static void SizeRowChildren(Element row)
    {
        //store the size of each cell on the grid element
        double cellWidth = row.Width / 12.0d;
        row.Parent!.AdditionalData = cellWidth;

        //size cells
        int colsUsed = 0;
        foreach (var cell in row.Children)
        {
            int colspan = Convert.ToInt32(cell.Tag.Name!.Split('-')[1]);
            cell.Width = colspan * cellWidth;
            if (cell.Width < MINIMUM_ELEMENT_WIDTH)
            {
                cell.Width = MINIMUM_ELEMENT_WIDTH;
            }
            cell.AdditionalData = new CellInfo
            {
                StartIndex = colsUsed,
                ColumnSpan = colspan
            };
            colsUsed += colspan;

            SizeCellChildren(cell);
        }
    }

    /// <summary>
    /// Sets the layout widths of children of a col-*, td, or th element
    /// </summary>
    /// <param name="cell">Cell element</param>
    private static void SizeCellChildren(Element cell)
    {
        //get left/right column padding to find available width
        double lateralPadding = 0;
        Func<Style?, double> getPaddingValue = (style) => {
            if (style != null)
            {
                var padVal = (NumericStyleValue)style.Value;
                return padVal.GetValueInInches();
            }
            else { return 0; }
        };
        var padding = cell.Styles.FirstOrDefault(s => s.Name == "padding-left");
        //padding only works when a border is present on the padded side...
        if (cell.Styles.Any(s => s.Name == "border-left-width"))
        {
            lateralPadding = getPaddingValue(padding);
        }
        padding = cell.Styles.FirstOrDefault(s => s.Name == "padding-right");
        if (cell.Styles.Any(s => s.Name == "border-right-width"))
        {
            lateralPadding += getPaddingValue(padding);
        }

        double availableWidth = cell.Width - lateralPadding;
        foreach (var element in cell.Children)
        {
            element.Width = GetDefaultElementWidth(element, availableWidth);
            SetExplicitElementWidth(element);
        }
    }

    /// <summary>
    /// Sets the layout widths of children of a table element
    /// </summary>
    /// <param name="table">Table element</param>
    private static void SizeTableChildren(Element table)
    {
        #region column count subroutines
        //notes how many columns a row's cell spans
        Func<Element, int, int> getCellSpanLength = (cell, columnIndex) =>
        {
            int colspan = 1;
            if (!string.IsNullOrWhiteSpace(cell.GetAttributeValue("colspan")))
            {
                int.TryParse(cell.GetAttributeValue("colspan"), out colspan);
                if (colspan < 1) { colspan = 1; }
            }

            cell.AdditionalData = new CellInfo
            {
                StartIndex = columnIndex,
                ColumnSpan = colspan
            };

            return colspan;
        };
        //gets the number of columns in a row
        Func<Element, int> getColumnsInRow = (row) =>
        {
            int colCount = 0;
            foreach (var cell in row.Children)
            {
                // also applies CellInfo to each cell
                colCount += getCellSpanLength(cell, colCount);
            }

            //attach row's column count to row
            row.AdditionalData = colCount;
            return colCount;
        };
        //gets the maximum number of columns in a row in a section
        Func<Element, int> getMaxColumnsInSection = (section) => {
            int maxRowLength = 0;
            foreach (var row in section.Children)
            {
                var l = getColumnsInRow(row);
                if (l > maxRowLength)
                {
                    maxRowLength = l;
                }
            }

            return maxRowLength;
        };
        #endregion

        //get column count
        int totalColumns = 0;
        foreach (var section in table.Children)
        {
            var maxRowLength = getMaxColumnsInSection(section);
            if (maxRowLength > totalColumns)
            {
                totalColumns = maxRowLength;
            }
        }

        #region column sizing subroutines
        //adds a row's defined width to an array of column widths
        Action<Element, double?[]> setCellColumnWidth = (cell, widthArray) =>
        {
            var styWidth = cell.Styles.FirstOrDefault(s => s.Name == "width");
            //nothing to do unless a width is defined for the cell
            if (styWidth == null) { return; }

            var value = (NumericStyleValue)styWidth.Value;
            var row = cell.Parent!;
            var section = row.Parent!;
            var tbl = section.Parent!;
            double width = value.GetValueInInches(tbl.Width);
            var ci = (CellInfo?)cell.AdditionalData!;
            // a width defined on a cell that spans multiple columns is divided equally
            // among the columns that it spans
            width = width / ci.ColumnSpan;

            //add this cell's defined width to the array of widths for its row
            int ln = ci.StartIndex + ci.ColumnSpan;
            for (int i = ci.StartIndex; i < ln; i++)
            {
                widthArray[i] = width;
            }
        };
        //gets an array of the widths defined in a row
        Func<Element, double?[]> getWidthsDefinedInRow = (row) =>
        {
            double?[] colWidths = new double?[totalColumns];
            foreach (var cell in row.Children)
            {
                setCellColumnWidth(cell, colWidths);
            }

            return colWidths;
        };
        //gets an array of the usable widths defined in a row
        Func<Element, double?[]> getWidthsDefinedInSection = (section) =>
        {
            double?[] colWidths = new double?[totalColumns];
            foreach (var row in section.Children)
            {
                var rowWidths = getWidthsDefinedInRow(row);
                for (int i = 0; i < totalColumns; i++)
                {
                    var masterValue = colWidths[i];
                    var rowValue = rowWidths[i];

                    //promote the row value if: a) the row value is defined but
                    //master value isn't, b) both row and master value are defined, and
                    //row value is greater than master value
                    if ((!masterValue.HasValue && rowValue.HasValue) ||
                        ((masterValue.HasValue && rowValue.HasValue) &&
                        (rowValue.Value > masterValue.Value)))
                    {
                        colWidths[i] = rowValue;
                    }
                }
            }

            return colWidths;
        };
        #endregion

        //get all column widths
        double?[] parseColumnWidths = new double?[totalColumns];
        foreach (var section in table.Children)
        {
            var sectionWidths = getWidthsDefinedInSection(section);
            for (int i = 0; i < totalColumns; i++)
            {
                var masterValue = parseColumnWidths[i];
                var rowValue = sectionWidths[i];

                //promote the row value if: a) the row value is defined but
                //master value isn't, b) both row and master value are defined, and
                //row value is greater than master value
                if ((!masterValue.HasValue && rowValue.HasValue) ||
                    ((masterValue.HasValue && rowValue.HasValue) &&
                    (rowValue.Value > masterValue.Value)))
                {
                    parseColumnWidths[i] = rowValue;
                }
            }
        }

        //make sure there are no columns with a defined width under the minimum,
        //while getting the total width used by columns with defined width, and
        //the indices of columns with undefined widths
        double allocatedWidth = 0.0d;
        List<int> columnsWithNoDefinedWidth = new List<int>();
        for (int i = 0; i < parseColumnWidths.Length; i++)
        {
            var cw = parseColumnWidths[i];
            if (cw.HasValue)
            {
                if (cw.Value < MINIMUM_ELEMENT_WIDTH)
                {
                    parseColumnWidths[i] = cw = MINIMUM_ELEMENT_WIDTH;
                }
                allocatedWidth += cw.Value;
            }
            else { columnsWithNoDefinedWidth.Add(i); }
        }

        //handle unused table width
        if (allocatedWidth < table.Width)
        {
            //if there are undefined column widths, the surplus is evenly divided among them
            if (columnsWithNoDefinedWidth.Any())
            {
                double widthPerCol =
                    (table.Width - allocatedWidth) / columnsWithNoDefinedWidth.Count;
                if (widthPerCol < MINIMUM_ELEMENT_WIDTH)
                {
                    widthPerCol = MINIMUM_ELEMENT_WIDTH;
                }

                foreach (var index in columnsWithNoDefinedWidth)
                {
                    parseColumnWidths[index] = widthPerCol;
                }
            }
            //if all column widths are already defined, divide surplus proportionately
            //among all columns
            else
            {
                double surplusWidth = table.Width - allocatedWidth;
                for (int i = 0; i < parseColumnWidths.Length; i++)
                {
                    var cw = parseColumnWidths[i]!;
                    double multiplier = cw.Value / allocatedWidth;
                    parseColumnWidths[i] = cw.Value + (surplusWidth * multiplier);
                }
            }
        }

        //make sure all columns have a defined width
        var columnWidths = parseColumnWidths
            .Select(x => x.HasValue ? x.Value : MINIMUM_ELEMENT_WIDTH).ToArray();

        //if the total column width is not within tolerance of table width,
        //resize columns to fit
        allocatedWidth = columnWidths.Sum();
        double delta = allocatedWidth - table.Width;
        if (delta > 0.01d)
        {
            //keep looping until resizing is complete
            while (true)
            {
                double totalMinimumWidths =
                    columnWidths.Where(c => c == MINIMUM_ELEMENT_WIDTH).Sum();
                double redistributableWidth = table.Width - totalMinimumWidths;
                //if there is no width to redistribute, all columns are the minimum width
                if (redistributableWidth <= 0)
                {
                    for (int i = 0; i < columnWidths.Length; i++)
                    {
                        columnWidths[i] = MINIMUM_ELEMENT_WIDTH;
                    }
                    break;
                }

                //if there is width to redistribute, distribute it among the columns
                //proportionately based on their previous values
                double totalResizableColumnWidth =
                    columnWidths.Where(c => c != MINIMUM_ELEMENT_WIDTH).Sum();
                var resizedColumns = new Dictionary<int, double>();
                for (int i = 0; i < columnWidths.Length; i++)
                {
                    //skip columns of minimum width
                    var cw = columnWidths[i];
                    if (cw == MINIMUM_ELEMENT_WIDTH) { continue; }

                    double multiplier = cw / totalResizableColumnWidth;
                    double colWidth = redistributableWidth * multiplier;
                    resizedColumns.Add(i, colWidth);
                }

                //if any of the resized columns are less than the minimum width,
                //set those columns to the minimum, and we'll recalculate the width of
                //the others
                bool recalculateWidths = false;
                var undersizedColumns =
                    resizedColumns.Where(kvp => kvp.Value < MINIMUM_ELEMENT_WIDTH);
                foreach (var kvp in undersizedColumns)
                {
                    columnWidths[kvp.Key] = kvp.Value;
                    recalculateWidths = true;
                }

                //if there's no need to recalculate widths, finalize column widths
                //and get out of this loop
                if (!recalculateWidths)
                {
                    foreach (var kvp in resizedColumns)
                    {
                        columnWidths[kvp.Key] = kvp.Value;
                    }
                    break;
                }
            }
        }

        //attach the column widths to the table, then size its cells
        table.AdditionalData = columnWidths;
        SizeTableCellChildren(table);
    }

    /// <summary>
    /// Sets the layout widths of the children of a table element's cells
    /// </summary>
    /// <param name="table">Table element</param>
    private static void SizeTableCellChildren(Element table)
    {
        var columnWidths = (double[]?)table.AdditionalData!;

        // thead/tbody/tfoot
        foreach (var section in table.Children)
        {
            // tr
            foreach (var row in section.Children)
            {
                // th/td
                foreach (var cell in row.Children)
                {
                    //get cell width, even across multiple columns 
                    var info = (CellInfo?)cell.AdditionalData!; // SizeTableChildren() adds this to every cell
                    cell.Width = 0;
                    for (int i = 0; i < info.ColumnSpan; i++)
                    {
                        cell.Width += columnWidths[info.StartIndex + i];
                    }

                    SizeCellChildren(cell);
                }
            }
        }
    }

    private const double MINIMUM_ELEMENT_WIDTH = 0.25d;
    private const double MINIMUM_PAGE_MARGIN = 0.25d;
}
