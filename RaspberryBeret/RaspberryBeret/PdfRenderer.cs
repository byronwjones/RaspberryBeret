using BWJ.Core;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using RaspberryBeret.Compilation;
using RaspberryBeret.DataBinding;
using RaspberryBeret.Elements;
using RaspberryBeret.ReferenceData;
using RaspberryBeret.Styling;
using System;
using System.IO;
using System.Linq;

namespace RaspberryBeret;

internal static class PdfRenderer
{
    /// <summary>
    /// Builds out a Document Object Model from a PDFML template that can be used to 
    /// render a PDF document
    /// </summary>
    /// <param name="model">Data model used for rendering PDF</param>
    /// <param name="pdfmlTemplate">Full path on disk, URL, or cloud resource reference, 
    /// to PDFML template used to render PDF</param>
    /// <param name="cloudService">Client object used to interact with an Azure
    /// Storage Account</param>
    public static Element BuildDOM(object? model, string pdfmlTemplate,
        ICloudResourceService? cloudService)
    {
        //create a model object for data binding
        model = model ?? new object();
        BindingModel bmodel = new BindingModel();
        bmodel.NameOfCurrentContext = "root";
        bmodel.ContextTree[bmodel.NameOfCurrentContext] = model;
        bmodel.CurrentContext = model;

        //load PDFML document contents
        string? pdfml = null;
        if (DataUtils.IsSourceCloudStorage(pdfmlTemplate))
        {
            if(cloudService is null)
            {
                throw new ArgumentException(nameof(cloudService), "Cloud service is required when the PDFML template is a cloud resource");
            }
            pdfml = DataUtils.GetTextFromCloudResource(pdfmlTemplate, cloudService);
        }
        else if(DataUtils.IsSourceWebBased(pdfmlTemplate))
        {
            pdfml = DataUtils.GetTextFromWeb(pdfmlTemplate);
        }
        else
        {
            using (StreamReader sr = new StreamReader(pdfmlTemplate))
            {
                pdfml = sr.ReadToEnd();
            }
        }

        //combine template and data into an object model for PDF rendering
        var dom = CompileUtils.PrecompilePDFML(pdfml, pdfmlTemplate, cloudService);
        CompileUtils.CompileDOM(dom, bmodel, cloudService);

        return dom;
    }

    /// <summary>
    /// Uses a DOM to create a PDF document object to save to disk, stream, or cloud
    /// </summary>
    /// <param name="dom">DOM used to create PDF</param>
    public static PdfDocument RenderPDF(Element dom)
    {
        //everything we are interested in is in the contents element
        var contentsElement = dom.Children.First(e => e.Tag.Name == TagName.contents);

        //begin PDF document
        Document document = new Document();
        var title = contentsElement.GetAttributeValue("title");
        if (Stringy.Relevant(title)) { document.Info.Title = title!; }
        var author = contentsElement.GetAttributeValue("author");
        if (Stringy.Relevant(author)) { document.Info.Author = author!; }
        var subject = contentsElement.GetAttributeValue("subject");
        if (Stringy.Relevant(subject)) { document.Info.Subject = subject!; }

        //render documents contained in PDF file
        foreach (var doc in contentsElement.Children)
        {
            RenderDocumentWithinPDF(doc, document);
        }

        //render PDF document
        var renderer = new PdfDocumentRenderer();
        renderer.Document = document;
        renderer.RenderDocument();

        //return document for saving
        return renderer.PdfDocument;
    }

    /// <summary>
    /// Renders the given PDFML document content element into a PDF section
    /// </summary>
    /// <param name="contentDocument">PDFML document element</param>
    /// <param name="document">Document on which to create PDF section</param>
    private static void RenderDocumentWithinPDF(Element contentDocument, Document document)
    {
        var s = document.AddSection();
        s.PageSetup = document.DefaultPageSetup.Clone();
        //by default, page number resets on every section
        if (!UseContinuousPageNumbering(contentDocument))
        { s.PageSetup.StartingNumber = 1; }
        //default header distance
        s.PageSetup.HeaderDistance = Unit.FromInch(0.5d);
        //set page orientation (only support 8.5" x 11")
        s.PageSetup.PageWidth = Unit.FromInch(8.5d);
        s.PageSetup.PageHeight = Unit.FromInch(11.0d);
        string orientation = contentDocument.GetAttributeValue("orientation") ?? "portrait";
        bool landscapeOrientation = orientation.ToLower() == "landscape";
        s.PageSetup.Orientation = landscapeOrientation ?
            Orientation.Landscape : Orientation.Portrait;

        //set page margins
        var topMargin = GetNumericStyleValue(contentDocument, "margin-top");
        var rightMargin = GetNumericStyleValue(contentDocument, "margin-right");
        var bottomMargin = GetNumericStyleValue(contentDocument, "margin-bottom");
        var leftMargin = GetNumericStyleValue(contentDocument, "margin-left");
        //default 1" margins (should always be explicitly defined, just a failsafe)
        s.PageSetup.TopMargin = UnitHasValue(topMargin) ?
            topMargin : Unit.FromInch(1.0d);
        s.PageSetup.RightMargin = UnitHasValue(rightMargin) ?
            rightMargin : Unit.FromInch(1.0d);
        s.PageSetup.BottomMargin = UnitHasValue(bottomMargin) ?
            bottomMargin : Unit.FromInch(1.0d);
        s.PageSetup.LeftMargin = UnitHasValue(leftMargin) ?
            leftMargin : Unit.FromInch(1.0d);

        //handle page numbering
        if (!string.IsNullOrWhiteSpace(contentDocument.GetAttributeValue("resetpagecount")))
        {
            s.PageSetup.StartingNumber = 1;
        }

        RenderHeadersAndFooters(contentDocument, s);

        //render the main contents of this document
        foreach (var e in contentDocument.Children)
        {
            if (e.Tag.Name == "grid")
            {
                RenderGrid(s, e);
            }
            else if (e.Tag.Name == "pagebreak")
            {
                s.AddPageBreak();
            }
            else if (e.Tag.Name == "table")
            {
                RenderTable(s, e);
            }
        }
    }

    /// <summary>
    /// Renders the headers and footers for the given PDF document section using
    /// the given PDFML document content element
    /// </summary>
    /// <param name="contentDocument">Content document DOM element</param>
    /// <param name="section">PDF document section to add headers/footers to</param>
    private static void RenderHeadersAndFooters(Element contentDocument, Section section)
    {
        //only need to do something if there are headers/footers defined
        if (!contentDocument.Children.Any(h => h.Tag.Name == TagName.header || h.Tag.Name == TagName.footer))
        {
            return;
        }

        section.PageSetup.OddAndEvenPagesHeaderFooter = true;
        section.PageSetup.DifferentFirstPageHeaderFooter = false;

        Func<Element, bool, string, bool> isHeadFootFor = (e, isHeader, targetpages) =>
        {
            string name = isHeader ? TagName.header : TagName.footer;
            var tpValue = e.GetAttributeValue("targetpages");
            tpValue = string.IsNullOrWhiteSpace(tpValue) ? "all" : tpValue;

            return e.Tag.Name == name && tpValue == targetpages;
        };

        Action<bool> applyHeadersOrFooters = (isHeader) =>
        {
            string elementName = isHeader ? "header" : "footer";
            var headsFeet = isHeader ? section.Headers : section.Footers;
            double addToHeaderOrFooter = 0.0d;

            if (contentDocument.Children.Any(h => h.Tag.Name == elementName))
            {
                var anyPage =
                    contentDocument.Children.LastOrDefault(h => isHeadFootFor(h, isHeader, "all"));
                var firstPage =
                    contentDocument.Children.LastOrDefault(h => isHeadFootFor(h, isHeader, "first"));
                var oddPage =
                    contentDocument.Children.LastOrDefault(h => isHeadFootFor(h, isHeader, "odd"));
                var evenPage =
                    contentDocument.Children.LastOrDefault(h => isHeadFootFor(h, isHeader, "even"));

                double reserveHeight;
                if (anyPage != null)
                {
                    headsFeet.Primary = RenderHeaderFooter(anyPage, out reserveHeight);
                    headsFeet.EvenPage = headsFeet.Primary.Clone();

                    if (addToHeaderOrFooter < reserveHeight)
                    {
                        addToHeaderOrFooter = reserveHeight;
                    }
                }
                if (oddPage != null)
                {
                    headsFeet.Primary =
                        RenderHeaderFooter(oddPage, out reserveHeight);
                    if (addToHeaderOrFooter < reserveHeight)
                    {
                        addToHeaderOrFooter = reserveHeight;
                    }
                }
                if (evenPage != null)
                {
                    headsFeet.EvenPage =
                        RenderHeaderFooter(evenPage, out reserveHeight);
                    if (addToHeaderOrFooter < reserveHeight)
                    {
                        addToHeaderOrFooter = reserveHeight;
                    }
                }
                if (firstPage != null)
                {
                    headsFeet.FirstPage =
                        RenderHeaderFooter(firstPage, out reserveHeight);
                    section.PageSetup.DifferentFirstPageHeaderFooter = true;

                    if (addToHeaderOrFooter < reserveHeight)
                    {
                        addToHeaderOrFooter = reserveHeight;
                    }
                }

                //if no height was specified on header/footer to reserve,
                //reserve 1" by default for header, 1/2" by default for footer
                if (addToHeaderOrFooter == 0)
                {
                    addToHeaderOrFooter = isHeader ? 1.0d : 0.5d;
                }
            }

            //if there was a header or footer included, modify the top or bottom
            //margin to accommodate the header or footer with the largest height
            if (addToHeaderOrFooter != 0)
            {
                if (isHeader)
                {
                    section.PageSetup.HeaderDistance =
                        Unit.FromInch(section.PageSetup.TopMargin.Inch);
                    section.PageSetup.TopMargin =
                        Unit.FromInch(section.PageSetup.TopMargin.Inch +
                        addToHeaderOrFooter);
                }
                else
                {
                    section.PageSetup.FooterDistance =
                        Unit.FromInch(section.PageSetup.BottomMargin.Inch);
                    section.PageSetup.BottomMargin =
                        Unit.FromInch(section.PageSetup.BottomMargin.Inch +
                        addToHeaderOrFooter);
                }
            }
        };

        //apply headers
        applyHeadersOrFooters(true);
        //apply footers
        applyHeadersOrFooters(false);
    }

    /// <summary>
    /// Renders a HeaderFooter PDF document object using a header or footer DOM element
    /// </summary>
    /// <param name="headFoot">Header or footer DOM element</param>
    /// <param name="reserveHeight">The amount (in inches) specified to add to 
    /// height of the top or bottom margin to accommodate the header or
    /// footer, respectively</param>
    /// <returns>HeaderFooter PDF document object</returns>
    private static HeaderFooter RenderHeaderFooter(Element headFoot, out double reserveHeight)
    {
        var result = new HeaderFooter();

        //get height to reserve for header/footer
        var rv = GetNumericStyleValue(headFoot, "height");
        reserveHeight = UnitHasValue(rv) ? rv.Value : 0.0d;

        //render header/footer contents
        foreach (var gridOrTable in headFoot.Children)
        {
            if (gridOrTable.Tag.Name == "grid")
            {
                RenderGrid(result, gridOrTable);
            }
            else
            {
                RenderTable(result, gridOrTable);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates and configures a PDF component that will be used as either a layout grid or table
    /// </summary>
    /// <param name="parent">The parent PDF section, header, or footer to add the
    /// layout grid or table to</param>
    /// <param name="eGridOrTable">Grid or table element from PDFML DOM containing rendering
    /// instructions</param>
    private static Table CreateGridOrTable(DocumentObject parent, Element eGridOrTable)
    {
        Table gridOrTable = new Table();

        // Migradoc doesn't support top/bottom margins for tables, so the workaround
        // is to add an empty paragraph above/below the table with the desired margin...
        Paragraph? topMargin = null;
        Paragraph? bottomMargin = null;
        //set table margins
        var mTop = GetNumericStyleValue(eGridOrTable, "margin-top");
        if (UnitHasValue(mTop))
        {
            topMargin = new Paragraph();
            topMargin.Format.LineSpacingRule = LineSpacingRule.Exactly;
            topMargin.Format.SpaceBefore = mTop;
        }
        //***note*** Whereas there is no explicitly defined right margin, during PDFML
        //DOM compilation we factor in the right margin value provided to calculate
        //the width of the table, effectively creating a right margin.  Moving on...
        var mBottom = GetNumericStyleValue(eGridOrTable, "margin-bottom");
        if (UnitHasValue(mBottom))
        {
            bottomMargin = new Paragraph();
            bottomMargin.Format.SpaceBefore = mBottom;
            bottomMargin.Format.LineSpacingRule = LineSpacingRule.Exactly;
        }
        var mLeft = GetNumericStyleValue(eGridOrTable, "margin-left");
        if (UnitHasValue(mLeft)) { gridOrTable.Rows.LeftIndent = mLeft; }

        // table parent must be an acceptable type
        if (parent.GetType() == typeof(Section))
        {
            var section = (Section)parent;
            if (topMargin != null) { section.Add(topMargin); }
            section.Add(gridOrTable);
            if (bottomMargin != null) { section.Add(bottomMargin); }
        }
        else if (parent.GetType() == typeof(HeaderFooter))
        {
            var headerFooter = (HeaderFooter)parent;
            if (topMargin != null) { headerFooter.Add(topMargin); }
            headerFooter.Add(gridOrTable);
            if (bottomMargin != null) { headerFooter.Add(bottomMargin); }
        }
        else { throw new ArgumentException("parent must be a Section or HeaderFooter"); }

        return gridOrTable;
    }

    /// <summary>
    /// Renders a PDF document layout grid and its contents
    /// </summary>
    /// <param name="parent">The parent PDF section, header, or footer to add the
    /// layout grid to</param>
    /// <param name="eGrid">Grid element from PDFML DOM containing rendering instructions</param>
    private static void RenderGrid(DocumentObject parent, Element eGrid)
    {
        Table grid = CreateGridOrTable(parent, eGrid);

        //a grid is just a table with exactly 12 columns we use for layout
        var colWidth = (double)eGrid.AdditionalData!;
        for (int i = 0; i < 12; i++)
        {
            grid.AddColumn(Unit.FromInch(colWidth));
        }

        //render rows
        foreach (var row in eGrid.Children)
        {
            RenderGridOrTableRow(grid, row);
        }
    }

    /// <summary>
    /// Renders a PDF document table and its contents
    /// </summary>
    /// <param name="parent">The parent PDF section, header, or footer to add the
    /// table to</param>
    /// <param name="eTable">Table element from PDFML DOM containing rendering instructions</param>
    private static void RenderTable(DocumentObject parent, Element eTable)
    {
        Table table = CreateGridOrTable(parent, eTable);

        //define table columns
        var colWidths = (double[]?)eTable.AdditionalData!;
        foreach (var cw in colWidths)
        {
            table.AddColumn(Unit.FromInch(cw));
        }

        //render table sections --thead
        var section = eTable.Children.FirstOrDefault(s => s.Tag.Name == "thead");
        if (section != null) { RenderTableSection(table, section); }
        //tbody is always present
        section = eTable.Children.First(s => s.Tag.Name == "tbody");
        RenderTableSection(table, section);
        //tfoot
        section = eTable.Children.FirstOrDefault(s => s.Tag.Name == "tfoot");
        if (section != null) { RenderTableSection(table, section); }
    }

    /// <summary>
    /// Renders a PDF document table and its contents
    /// </summary>
    /// <param name="parent">The parent PDF table to add the section to</param>
    /// <param name="eSection">Section element from PDFML DOM containing rendering instructions</param>
    private static void RenderTableSection(Table parent, Element eSection)
    {
        bool isTableHead = eSection.Tag.Name == "thead";
        //render section rows
        foreach (var row in eSection.Children)
        {
            RenderGridOrTableRow(parent, row, isTableHead);
        }
    }

    /// <summary>
    /// Renders a PDF document layout grid row and its contents
    /// </summary>
    /// <param name="grid">The layout grid to render the row to</param>
    /// <param name="eRow">Grid row PDFML DOM element containing rendering
    /// instructions</param>
    private static void RenderGridOrTableRow(Table grid, Element eRow, bool isTableHeaderRow = false)
    {
        var row = grid.AddRow();
        row.HeadingFormat = isTableHeaderRow;
        //apply background color to row
        var bgColor = GetColorValue(eRow, "background-color");
        if (bgColor.HasValue) { row.Shading.Color = bgColor.Value; }

        //render cells in row
        foreach (var cell in eRow.Children)
        {
            //render cell styling and contents
            RenderCell(row, cell);
        }
    }

    /// <summary>
    /// Renders a PDF table or layout grid cell and its contents
    /// </summary>
    /// <param name="row">The row the cell being rendered is a part of</param>
    /// <param name="eCell">Table or layout grid cell PDFML DOM element containing
    /// rendering instructions</param>
    private static void RenderCell(Row row, Element eCell)
    {
        //use cell info embedded in PDFML cell element to find out which cell to start in,
        //and how many columns to span
        var ci = (CellInfo?)eCell.AdditionalData!;
        var cell = row.Cells[ci.StartIndex];
        //merge cells as needed
        if (ci.ColumnSpan > 1)
        {
            cell.MergeRight = ci.ColumnSpan - 1;
        }

        //apply horizontal text alignment
        var textAlignment = GetStringValue(eCell, "text-align");
        switch (textAlignment)
        {
            case "left":
                cell.Format.Alignment = ParagraphAlignment.Left;
                break;
            case "center":
                cell.Format.Alignment = ParagraphAlignment.Center;
                break;
            case "right":
                cell.Format.Alignment = ParagraphAlignment.Right;
                break;
            case "justify":
                cell.Format.Alignment = ParagraphAlignment.Justify;
                break;
        }

        //apply vertical alignment
        var verticalAlignment = GetStringValue(eCell, "vertical-align");
        switch (textAlignment)
        {
            case "top":
                cell.VerticalAlignment = VerticalAlignment.Top;
                break;
            case "middle":
                cell.VerticalAlignment = VerticalAlignment.Center;
                break;
            case "bottom":
                cell.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }

        //apply borders and padding, but only to table cells, which always have a
        //tr element parent in the PDFML DOM
        if (eCell.Parent!.Tag.Name == "tr")
        {
            ApplyBorders(cell.Borders, eCell);
        }

        //apply background color
        var bgColor = GetColorValue(eCell, "background-color");
        if (bgColor.HasValue) { cell.Shading.Color = bgColor.Value; }

        //render cell contents
        foreach (var content in eCell.Children)
        {
            switch (content.Tag.Name)
            {
                case "p":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    RenderParagraphOrHeading(cell, content);
                    break;
                case "img":
                    RenderImage(cell, content);
                    break;
            }
        }
    }

    /// <summary>
    /// Renders a PDF paragraph and its contents
    /// </summary>
    /// <param name="cell">The table or layout grid cell the paragraph is being
    /// rendered to</param>
    /// <param name="ePgraph">Paragraph or heading PDFML DOM element containing
    /// rendering instructions</param>
    private static void RenderParagraphOrHeading(Cell cell, Element ePgraph)
    {
        var pgraph = cell.AddParagraph();

        //apply margins
        Unit zero = new Unit(0.0, UnitType.Inch);
        var mTop = GetNumericStyleValue(ePgraph, "margin-top");
        if (UnitHasValue(mTop)) { pgraph.Format.SpaceBefore = mTop; }
        var mRight = GetNumericStyleValue(ePgraph, "margin-right");
        if (UnitHasValue(mRight)) { pgraph.Format.RightIndent = mRight; }
        var mBottom = GetNumericStyleValue(ePgraph, "margin-bottom");
        if (UnitHasValue(mBottom)) { pgraph.Format.SpaceAfter = mBottom; }
        var mLeft = GetNumericStyleValue(ePgraph, "margin-left");
        if (UnitHasValue(mLeft)) { pgraph.Format.LeftIndent = mLeft; }

        //apply borders and padding
        ApplyBorders(pgraph.Format.Borders, ePgraph);

        //apply background color
        var bgColor = GetColorValue(ePgraph, "background-color");
        if (bgColor.HasValue) { pgraph.Format.Shading.Color = bgColor.Value; }

        //apply line spacing
        var lHeight = GetNumericStyleValue(ePgraph, "line-height");
        if (UnitHasValue(lHeight))
        {
            pgraph.Format.LineSpacingRule = LineSpacingRule.AtLeast;
            pgraph.Format.LineSpacing = lHeight;
        }

        //apply indentation
        var tIndent = GetNumericStyleValue(ePgraph, "text-indent");
        if (UnitHasValue(tIndent)) { pgraph.Format.FirstLineIndent = tIndent; }

        //apply lateral alignment
        var textAlignment = GetStringValue(ePgraph, "text-align");
        switch (textAlignment)
        {
            case "left":
                pgraph.Format.Alignment = ParagraphAlignment.Left;
                break;
            case "center":
                pgraph.Format.Alignment = ParagraphAlignment.Center;
                break;
            case "right":
                pgraph.Format.Alignment = ParagraphAlignment.Right;
                break;
            case "justify":
                pgraph.Format.Alignment = ParagraphAlignment.Justify;
                break;
        }

        RenderTextBlockContents(pgraph, ePgraph);
    }

    /// <summary>
    /// Renders an image
    /// </summary>
    /// <param name="cell">The table or layout grid cell the paragraph is being
    /// rendered to</param>
    /// <param name="eImage">Image PDFML DOM element containing rendering
    /// instructions</param>
    private static void RenderImage(Cell cell, Element eImage)
    {
        var image = cell.AddImage(eImage.GetAttributeValue("src")!);

        //apply margins -- only top and left are supported
        var mTop = GetNumericStyleValue(eImage, "margin-top");
        if (UnitHasValue(mTop))
        {
            image.RelativeVertical = MigraDoc.DocumentObjectModel.Shapes.RelativeVertical.Line;
            image.Top = mTop;
        }
        var mLeft = GetNumericStyleValue(eImage, "margin-left");
        if (UnitHasValue(mLeft))
        {
            image.RelativeHorizontal = MigraDoc.DocumentObjectModel.Shapes.RelativeHorizontal.Column;
            image.Left = mLeft;
        }

        //size image
        bool hasWidth = eImage.Styles.Any(s => s.Name == "width");
        bool hasHeight = eImage.Styles.Any(s => s.Name == "height");
        //use element width if width was explicitly set, or if neither width nor
        //height was explicitly set
        if (hasWidth || (!hasWidth && !hasHeight))
        {
            image.Width = new Unit(eImage.Width, UnitType.Inch);
        }
        var height = GetNumericStyleValue(eImage, "height");
        if (UnitHasValue(height)) { image.Height = height; }
    }

    private static void RenderTextBlockContents(Paragraph textblock, Element tbElement)
    {
        foreach (var inlineText in tbElement.Children)
        {
            if (inlineText.Tag.Name == "br")
            {
                textblock.AddLineBreak();
            }
            else
            {
                RenderInlineText(textblock.AddFormattedText(), inlineText);
            }
        }
    }

    /// <summary>
    /// Styles and renders elements found in a text block to the PDF document
    /// </summary>
    /// <param name="text">Inline text to style and render</param>
    /// <param name="textElement">PDFML element used to control styling/rendering</param>
    private static void RenderInlineText(FormattedText text, Element textElement)
    {
        FormattedText currentText = text;
        bool processChildren = false;

        //handling rendering based on PDFML element type
        switch (textElement.Tag.Name)
        {
            case "br":
                currentText.AddLineBreak();
                break;

            case "pagetotal":
                //apply styles for text
                DecorateText(currentText, textElement);

                MigraDoc.DocumentObjectModel.Fields.NumericFieldBase? ptField = null;
                if (UseContinuousPageNumbering(textElement))
                {
                    ptField = currentText.AddNumPagesField();
                }
                else
                {
                    ptField = currentText.AddSectionPagesField();
                }

                string ptFormat = textElement.GetAttributeValue("format") ?? string.Empty;
                ptFormat = ptFormat.ToLower();
                switch (ptFormat)
                {
                    case "roman":
                    case "roman-upper":
                        ptField.Format = "ROMAN";
                        break;
                    case "roman-lower":
                        ptField.Format = "roman";
                        break;
                    case "abc":
                    case "abc-upper":
                        ptField.Format = "ALPHABETIC";
                        break;
                    case "abc-lower":
                        ptField.Format = "alphabetic";
                        break;
                }
                break;

            case "pagenumber":
                //apply styles for text
                DecorateText(currentText, textElement);

                MigraDoc.DocumentObjectModel.Fields.NumericFieldBase pnField =
                    currentText.AddPageField();

                string pnFormat = textElement.GetAttributeValue("format") ?? string.Empty;
                pnFormat = pnFormat.ToLower();
                switch (pnFormat)
                {
                    case "roman":
                    case "roman-upper":
                        pnField.Format = "ROMAN";
                        break;
                    case "roman-lower":
                        pnField.Format = "roman";
                        break;
                    case "abc":
                    case "abc-upper":
                        pnField.Format = "ALPHABETIC";
                        break;
                    case "abc-lower":
                        pnField.Format = "alphabetic";
                        break;
                }
                break;

            case "textrun":
                currentText = currentText.AddFormattedText(textElement.InnerText);
                DecorateText(currentText, textElement);
                break;

            case "a":
                var href = textElement.GetAttributeValue("href");
                if (string.IsNullOrWhiteSpace(href)) { href = "#"; }
                var link = currentText.AddHyperlink(href);
                link.Type = href == "#" ? HyperlinkType.Local : HyperlinkType.Url;
                currentText = link.AddFormattedText();
                processChildren = true;
                break;
            default:
                processChildren = true;
                break;
        }

        //if need be, call this method for children of the current PDFML element
        if (processChildren)
        {
            foreach (var child in textElement.Children)
            {
                RenderInlineText(currentText, child);
            }
        }
    }

    /// <summary>
    /// Applies styling to a run of text
    /// </summary>
    /// <param name="text">Run of text to apply styling to</param>
    /// <param name="textElement">PDFML textrun element</param>
    private static void DecorateText(FormattedText text, Element textElement)
    {
        var font = GetStringValue(textElement, "font-family");
        if (!string.IsNullOrWhiteSpace(font)) { text.Font = new Font(font); }

        var clr = GetColorValue(textElement, "color");
        if (clr.HasValue) { text.Color = clr.Value; }

        var fSize = GetNumericStyleValue(textElement, "font-size");
        if (UnitHasValue(fSize)) { text.Size = Unit.FromPoint(fSize.Value * 72.0d); }

        var weight = GetStringValue(textElement, "font-weight");
        switch (weight)
        {
            case "normal":
                text.Bold = false;
                break;
            case "bold":
                text.Bold = true;
                break;
        }

        var italic = GetStringValue(textElement, "font-style");
        switch (italic)
        {
            case "normal":
                text.Italic = false;
                break;
            case "italic":
                text.Italic = true;
                break;
        }

        var uline = GetStringValue(textElement, "text-decoration");
        switch (uline)
        {
            case "none":
                text.Underline = Underline.None;
                break;
            case "underline":
                text.Underline = Underline.Single;
                break;
        }
    }

    /// <summary>
    /// Applies borders defined in styles to the given PDF entity border object
    /// </summary>
    /// <param name="b">PDF document entity border object</param>
    /// <param name="e">PDFML DOM element containing border styles</param>
    private static void ApplyBorders(Borders b, Element e)
    {
        ApplyBorderAndPadding("top", b, e);
        ApplyBorderAndPadding("right", b, e);
        ApplyBorderAndPadding("bottom", b, e);
        ApplyBorderAndPadding("left", b, e);
    }

    /// <summary>
    /// Applies border appearance defined in styles for a specific side
    /// to the given PDF entity border object
    /// </summary>
    /// <param name="side">Border side to apply styles for</param>
    /// <param name="b">PDF document entity border object</param>
    /// <param name="e">PDFML DOM element containing border styles</param>
    private static void ApplyBorderAndPadding(string side, Borders b, Element e)
    {
        string widthStyleName = String.Format("border-{0}-width", side);
        string styleStyleName = String.Format("border-{0}-style", side);
        string colorStyleName = String.Format("border-{0}-color", side);
        string paddingStyleName = String.Format("padding-{0}", side);

        //get border being applied to
        Border? border = null;
        switch (side)
        {
            case "top":
                border = b.Top;
                break;
            case "right":
                border = b.Right;
                break;
            case "bottom":
                border = b.Bottom;
                break;
            case "left":
                border = b.Left;
                break;
            default: //illegal (should never happen though)
                return;
        }

        var width = GetNumericStyleValue(e, widthStyleName);
        if (UnitHasValue(width)) { border.Width = width; }
        else { return; } // border must have width to have anything else


        var style = GetStringValue(e, styleStyleName);
        switch (style)
        {
            case "solid":
                border.Style = BorderStyle.Single;
                break;
            case "dashed":
                border.Style = BorderStyle.DashSmallGap;
                break;
            case "dotted":
                border.Style = BorderStyle.Dot;
                break;
            default:
                border.Style = BorderStyle.Single;
                break;
        }

        var color = GetColorValue(e, colorStyleName);
        if (color.HasValue) { border.Color = color.Value; }

        //set padding if necessary
        var padding = GetNumericStyleValue(e, paddingStyleName);
        if (UnitHasValue(padding))
        {
            switch (side)
            {
                case "top":
                    b.DistanceFromTop = padding;
                    break;
                case "right":
                    b.DistanceFromRight = padding;
                    break;
                case "bottom":
                    b.DistanceFromBottom = padding;
                    break;
                case "left":
                    b.DistanceFromLeft = padding;
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the string value for the style of the given name associated
    /// with the given element
    /// </summary>
    /// <param name="e">Element containing style of interest</param>
    /// <param name="styleName">Name of style of interest</param>
    /// <returns>String value, if present</returns>
    private static string? GetStringValue(Element e, string styleName)
    {
        var style = e.Styles.FirstOrDefault(s => s.Name == styleName);
        if (style == null) { return null; }

        var value = style.Value as StringStyleValue;
        if (value == null) { return null; }

        return value.Value;
    }

    /// <summary>
    /// Gets the color value for the style of the given name associated
    /// with the given element
    /// </summary>
    /// <param name="e">Element containing style of interest</param>
    /// <param name="styleName">Name of style of interest</param>
    /// <returns>Color value, if present</returns>
    private static Color? GetColorValue(Element e, string styleName)
    {
        var style = e.Styles.FirstOrDefault(s => s.Name == styleName);
        if (style == null) { return null; }

        var value = style.Value as ColorStyleValue;
        if (value == null) { return null; }

        return Color.Parse("0xFF" + value.Value);
    }

    /// <summary>
    /// Gets the numeric value (in inches) for the style of the given name associated
    /// with the given element
    /// </summary>
    /// <param name="e">Element containing style of interest</param>
    /// <param name="styleName">Name of style of interest</param>
    /// <returns>Value for style in inches, if present</returns>
    private static Unit GetNumericStyleValue(Element e, string styleName)
    {
        var NO_VALUE = Unit.FromPoint(-0.001d);

        var style = e.Styles.FirstOrDefault(s => s.Name == styleName);
        if (style == null) { return NO_VALUE; }

        var value = style.Value as NumericStyleValue;
        if (value == null) { return NO_VALUE; }

        return Unit.FromInch(value.GetValueInInches());
    }

    /// <summary>
    /// Tests whether or not the given unit is valueless - by our definition,
    /// a unit with no value is less than -0.01 in point units
    /// </summary>
    /// <param name="u">Unit object to test</param>
    /// <returns>True if given unit has value, false if not</returns>
    private static bool UnitHasValue(Unit u)
    {
        return u.Value < -0.01d ||
            u.Type != UnitType.Point;
    }

    /// <summary>
    /// Finds the contents element of the DOM using the given element, and uses it to
    /// determine if the PDF being rendered is using continuous page numbering
    /// (i.e. page numbers do not reset on new sections, aka content documents)
    /// </summary>
    /// <param name="e">Element of DOM being used to render a PDF document</param>
    /// <returns>True if continuous page numbering is being implemented, else false</returns>
    private static bool UseContinuousPageNumbering(Element e)
    {
        if (e == null || e.RootParent is null) { return false; } //just in case...

        //get the contents element
        var contents = e.RootParent.Children.First(d => d.Tag.Name == TagName.contents);
        var numberingMode = contents.GetAttributeValue("page-numbering");

        //resetting page numbers for every section is the default behavior
        if (Stringy.Empty(numberingMode)) { return false; }
        return numberingMode!.ToLower().Trim() == "continuous";
    }
}