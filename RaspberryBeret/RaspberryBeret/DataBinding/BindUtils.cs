using BWJ.Core;
using RaspberryBeret.Elements;
using RaspberryBeret.Parsing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RaspberryBeret.DataBinding;
internal static class BindUtils
{
    /// <summary>
    /// Applies the given binding model to the given element and its children
    /// recursively
    /// </summary>
    /// <param name="model">Binding model to apply to element</param>
    /// <param name="element">Element to apply binding model to</param>
    public static void ApplyBindingModel(BindingModel model, Element element)
    {
        element.DataModel = model;
        //if this element defines a context, apply a new model to it
        var context = element.GetAttributeValue("context");
        if (!string.IsNullOrEmpty(context))
        {
            try
            {
                element.DataModel = CreateBindingModel(context, model);
            }
            catch (Exception e)
            {
                ParseUtils.ThrowParsingException(element, e.Message, e);
            }
        }

        //apply the parent's model to its children
        foreach (var child in element.Children)
        {
            ApplyBindingModel(element.DataModel, child);
        }
    }

    /// <summary>
    /// Resolves a binding expression string to a value from the given binding model
    /// </summary>
    /// <param name="expression">The binding expression</param>
    /// <param name="model">The binding model used to resolve the given expression</param>
    /// <returns>The value that the binding expression resolves to</returns>
    public static string ResolveBindingExpression(string expression, BindingModel model)
    {
        //strip curly braces from binding expression
        expression = Regex.Replace(expression, @"(^\{\{\s*)|(\s*\}\}$)", string.Empty, RegexOptions.IgnoreCase);
        //get format portion of binding expression, if present
        string fmtPart = @"\|\s*([^\s`]*|`[^`]*`)$";
        Match fmtMatch = Regex.Match(expression, fmtPart);
        string? format = null;
        if (fmtMatch.Success)
        {
            expression = expression.Remove(fmtMatch.Index).Trim();
            format = fmtMatch.Value.Remove(0, 1).Trim();//removed pipe character
            if (isStringLiteral(format))
            {
                format = unwrapStringLiteral(format);
            }
            else
            {
                format = StringifyValue(ResolveModelReference(format, model));
            }

            if (string.IsNullOrEmpty(format)) { format = null; }
        }

        object? value = null;
        //handle simple binding containing just a model reference
        if (Regex.IsMatch(expression, @"^@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*$", RegexOptions.IgnoreCase))
        {
            value = ResolveModelReference(expression, model);
        }
        //handle complex conditional binding
        else
        {
            value = resolveConditionalModelReference(expression, model);
        }

        return StringifyValue(value, format);
    }

    /// <summary>
    /// Converts a 'truthy' value to a definitive boolean value
    /// </summary>
    /// <param name="value">'Truthy' value</param>
    /// <returns>Boolean value</returns>
    public static bool ResolveTruthiness(object? value)
    {
        //null values are false
        if (value == null) { return false; }

        //handle boolean value
        if (value.GetType() == typeof(bool))
        {
            return (bool)value;
        }

        //handle string values
        if (value.GetType() == typeof(string))
        {
            var str = value.ToString()?.ToLower() ?? string.Empty;
            //empty string is false
            if (string.IsNullOrWhiteSpace(str))
            {
                return false;
            }
            if(double.TryParse(str, out var d))
            {
                if(d == 0) {  return false; }
            }

            //any of the foregoing values is 'falsey'
            var @false = new string[] { "false", "f", "no", "n" };
            return !@false.Contains(str);
        }

        //handle types that implement IConvertible -- these types determine
        //the criteria for true or false themselves
        if (typeof(IConvertible).IsAssignableFrom(value.GetType()))
        {
            try
            {
                var v = Convert.ToBoolean(value);
                return v;
            }
            catch
            {
                //default true, since value is not null
                return true;
            }
        }

        //for any other type, any non null value is true
        return true;
    }

    /// <summary>
    /// Provides the string representation of a given value.  This is essentially done
    /// by invoking the ToString method on the given value, or returning an empty string
    /// if the given value is null
    /// </summary>
    /// <param name="value">Value to stringify</param>
    /// <param name="format">Optional format specifier to use when invoking ToString method</param>
    /// <returns>String representation of the given value</returns>
    public static string StringifyValue(object? value, string? format = null)
    {
        //null values result in an empty string
        if (value == null) { return string.Empty; }

        //handle simplest case: no format provided
        if (string.IsNullOrWhiteSpace(format))
        {
            return value.ToString() ?? string.Empty;
        }

        //if we are here, a format was provided. Verify that the value type supports
        //providing a format
        var type = value.GetType();
        var mi = type.GetMethod("ToString", [typeof(string)]);
        if (mi != null)
        {
            //call the ToString overload
            try
            {
                var str = mi.Invoke(value, [format]);
                return str?.ToString() ?? string.Empty;
            }
            catch
            {
                //if the format provided is bad, an exception may be thrown.
                //in this case, just return using the default ToString method
                return value.ToString() ?? string.Empty;
            }
        }
        // no format specifier provided, so just use the default ToString method
        else
        {
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the number of members in an enumerable
    /// </summary>
    /// <param name="obj">Enumerable object</param>
    /// <returns>The number of members in the collection if the given object is
    /// an enumerable, else -1</returns>
    public static int GetEnumerableCount(object obj)
    {
        if (obj == null) { return -1; }

        //object passed in is a list
        if (typeof(IList).IsAssignableFrom(obj.GetType()))
        {
            var list = (IList)obj;
            return list.Count;
        }
        //object is a string
        else if (obj.GetType() == typeof(string))
        {
            return ((string)obj).Length ;
        }
        //object is an enumerable
        else if (typeof(IEnumerable).IsAssignableFrom(obj.GetType()))
        {
            var iEnum = (IEnumerable)obj;
            //get member count
            int inc = 0;
            foreach (var item in iEnum) { inc++; }
            return inc;
        }

        //object is not enumerable
        return -1;
    }

    /// <summary>
    /// Resolves a string referencing a property or method within the scope of a
    /// given model to the value returned by that property or method
    /// </summary>
    /// <param name="modelRef">Model reference string</param>
    /// <param name="model">Binding model used to resolve the given reference</param>
    /// <param name="allowExplicitContextReference">
    /// If true (default) the first property reference in the statement is considered a potential reference to a specific context
    /// in the current context hierarchy. If false, it is only evaluated as a property of the current context object
    /// </param>
    /// <returns>The value to which the property or method referred to resolves</returns>
    public static object? ResolveModelReference(string modelRef, BindingModel model, bool allowExplicitContextReference = true)
    {
        MethodGuard.NoNull(new {modelRef, model});
        if (!Regex.IsMatch(modelRef, @"^@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*$", RegexOptions.IgnoreCase))
        {
            throw new FormatException("Model reference '" + modelRef + "' is invalid.");
        }

        int startIndex = 0;
        object? property = null;

        //split the model reference into context and/or individual property
        //references
        string[] propRefs = modelRef.Split('.');

        //determine if the first reference is to a context in the model
        if (allowExplicitContextReference && containsContextReference(modelRef, model))
        {
            property = ResolveContextTreeReference(propRefs[0], model);
            //parse property references starting at index 1
            startIndex = 1;
        }
        //if context was not explicitly referred to, it is implied that the current
        //context is being used
        else
        {
            property = model.CurrentContext;
        }

        //if context object isn't null, resolve the rest of the properties in the model path
        if (property != null)
        {
            for (int inc = startIndex; inc < propRefs.Length; inc++)
            {
                var propStr = propRefs[inc];
                property = resolveObjectPropertyReference(propStr, property, model);

                if (property == null) { break; }
            }
        }

        return property;
    }

    public static void ValidateContextDeclaration(string contextDeclaration)
    {
        MethodGuard.NoNull(new {contextDeclaration});
        if (!Regex.IsMatch(contextDeclaration, @"^@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*\s+as\s+@?[a-z_][a-z0-9_]*$", RegexOptions.IgnoreCase))
        {
            throw new FormatException("Context declaration '" + contextDeclaration +
                "' is invalid.");
        }
    }

    /// <summary>
    /// Creates a binding model based on a parent model with a new context definition
    /// </summary>
    /// <param name="contextDeclaration">Declaration of model's new context</param>
    /// <param name="parentModel">Binding model (associated with an element higher up a DOM
    /// hierarchy) to use to create the new model</param>
    /// <returns>New binding model</returns>
    public static BindingModel CreateBindingModel(string contextDeclaration,
        BindingModel parentModel)
    {
        MethodGuard.NoNull(new { parentModel });
        contextDeclaration = contextDeclaration ?? string.Empty;
        ValidateContextDeclaration(contextDeclaration);

        //get context declaration components
        contextDeclaration = Regex.Replace(contextDeclaration, @"\s+", " ");
        var cdParts = contextDeclaration.Split(' ');
        var modelRef = cdParts[0].Trim();
        var contextName = cdParts[2].Trim();//because index 1 is 'as'

        //make sure context alias unique
        if (parentModel.ContextTree.ContainsKey(contextName))
        {
            throw new ArgumentException("Cannot name context '" + contextName +
                "': another context by the same name was already declared on one of this element's parents.");
        }

        //create model
        var model = new BindingModel();
        model.NameOfCurrentContext = contextName;
        var currentContext = ResolveModelReference(modelRef, parentModel, allowExplicitContextReference: false) ?? new object();
        model.CurrentContext = currentContext;
        foreach (var cxt in parentModel.ContextTree)
        {
            model.ContextTree[cxt.Key] = cxt.Value;
        }
        model.ContextTree[contextName] = model.CurrentContext;

        return model;
    }

    /// <summary>
    /// Determines whether or not the given text contains a binding expression
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <returns>True if the given text contains a binding expression, else false</returns>
    public static bool ContainsBindingExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return false; }
        return Regex.IsMatch(text, BINDING_EXPRESSION_REGEX, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Composes a list of all the unique binding expressions found in the given string
    /// </summary>
    /// <param name="text">Text from which to extract binding expressions</param>
    /// <returns>An array of the unique binding expressions encountered</returns>
    public static string[] ExtractBindingExpressions(string text)
    {
        HashSet<string> exprs = new HashSet<string>();
        var matches = Regex.Matches(text, BINDING_EXPRESSION_REGEX, RegexOptions.IgnoreCase)
            .Cast<Match>();
        foreach (var m in matches)
        {
            exprs.Add(m.Value);
        }

        return exprs.ToArray();
    }

    /// <summary>
    /// Resolves any binding references contained in the given text
    /// </summary>
    /// <param name="text">Input text with binding references</param>
    /// <param name="model">Data model used to resolve binding references</param>
    /// <returns>Input text with all binding references resolved</returns>
    public static string BindTextString(string text, BindingModel model)
    {
        string result = text;
        if (ContainsBindingExpression(text))
        {
            var expressions = ExtractBindingExpressions(text);
            foreach (var expr in expressions)
            {
                var boundText = ResolveBindingExpression(expr, model);
                result = result.Replace(expr, boundText);
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether or not the given statement represents a string literal,
    /// which are wrapped with grave accent (`) characters
    /// </summary>
    /// <param name="statement">Statement to analyze</param>
    /// <returns>True if the statement is a string literal</returns>
    private static bool isStringLiteral(string statement)
    {
        statement = statement.Trim();
        return Regex.IsMatch(statement, @"^`[^`]*`$");
    }

    /// <summary>
    /// Remove accent grave characters encasing a statement that represents a literal
    /// string
    /// </summary>
    /// <param name="literal">String literal</param>
    /// <returns>Unwrapped string literal</returns>
    private static string unwrapStringLiteral(string literal)
    {
        return literal.Trim()
            .Remove(0, 1)
            .Remove(literal.Length - 2);
    }

    /// <summary>
    /// Determines whether the given predicate expression evaluates to true, and
    /// outputs the value to bind to the template when the predicate is true
    /// </summary>
    /// <param name="predicate">Predicate statement to evaluate</param>
    /// <param name="model">Binding model used to evaluate the predicate expression</param>
    /// <param name="value">The value to bind to the template if the predicate
    /// evaluates to true</param>
    /// <returns>True if the predicate expression evaluates to true</returns>
    private static bool resolvePredicateExpression(string predicate, BindingModel model,
        out object? value)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            throw new ArgumentNullException("predicate is null or empty");
        }
        if (model == null)
        {
            throw new ArgumentNullException("model is null");
        }

        predicate = predicate.Trim();
        bool desiredEvalResult = predicate.IndexOf("ifnot") == 0 ? false : true;

        //strip unneeded chars
        predicate = Regex.Replace(predicate, @"(^if(not)?\s*\(\s*)|(\s*\)$)",
            string.Empty, RegexOptions.IgnoreCase);
        //split into eval expression , binding expression components
        int commaIdx = predicate.IndexOf(',');
        string evalExpr = predicate.Substring(0, commaIdx).Trim();
        string bindExpr = predicate.Substring(commaIdx + 1).Trim();

        //resolve expressions
        bool evalResult = ResolveTruthiness(ResolveModelReference(evalExpr, model));
        value = isStringLiteral(bindExpr) ?
            unwrapStringLiteral(bindExpr) : ResolveModelReference(bindExpr, model);

        return evalResult == desiredEvalResult;
    }

    /// <summary>
    /// Resolves a string referencing a property or method on a given instance to 
    /// the value of that property on the instance
    /// </summary>
    /// <param name="propRef">Property reference string</param>
    /// <param name="host">Instance containing the desired property value</param>
    /// <param name="model">Binding model used to resolve a method reference</param>
    /// <returns>The value to which the property or method referred to resolves</returns>
    private static object? resolveObjectPropertyReference(string propRef, object host,
        BindingModel model)
    {

        MethodGuard.NoNull(new { propRef, model });
        //if host object is null, property automatically resolves to null
        if (host == null)
        {
            return null;
        }

        //get base property reference
        string propName =
            Regex.Match(propRef, "^@?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase).Value;
        object? property = null;

        //resolve by invoking a method, if necessary
        var mi = getBindingMethodInfo(propName, host);
        if (mi != null)
        {
            property = resolveBindingMethod(mi, host, model);
        }
        //since its not a method, its likely a property
        else
        {
            try
            {
                var pi = host.GetType().GetProperty(propName);
                property = pi?.GetValue(host);

                //if the property value is method with signature:
                // object method(BindingModel bm),
                //we'll invoke it to get the true property value
                if (property != null &&
                    property.GetType() == typeof(Func<BindingModel, object?>))
                {
                    var fn = (Func<BindingModel, object?>)property;
                    property = fn(model);
                }
            }
            catch
            {
                property = null;
            }
        }

        //resolve collection member references, if necessary
        if (property != null)
        {
            //get any indices referred to
            var indices = getCollectionIndices(propRef);
            //resolve property value to member referred to in parent collection
            foreach (var idx in indices)
            {
                property = resolveIndexedProperty(property, idx);
                if (property == null) { break; }
            }
        }

        return property;
    }

    /// <summary>
    /// Resolves a string containing one or more predicate expressions to a value
    /// in the given binding model
    /// </summary>
    /// <param name="condRef">Conditional reference string</param>
    /// <param name="model">Binding model to use to resolve the given expression</param>
    /// <returns>The resolved value</returns>
    private static object? resolveConditionalModelReference(string condRef,
        BindingModel model)
    {
        //get a list of if/not predicates in the conditional property reference
        var predicates = Regex.Matches(condRef, @"if(not)?\s*\(\s*(@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)\s*\,\s*((@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)|`[^`]*`)\s*\)", RegexOptions.IgnoreCase)
            .Cast<Match>().ToList();
        //extract the default value reference, if any
        var lastMatch = predicates.Last();
        int defStartIndex = lastMatch.Index + lastMatch.Length;
        string? defRef = null;
        if (defStartIndex < condRef.Length)
        {
            defRef = condRef.Substring(defStartIndex).Trim();
        }

        //try resolving value from predicates
        bool resolved = false;
        object? result = null;
        foreach (var p in predicates)
        {
            if (resolvePredicateExpression(p.Value, model, out result))
            {
                resolved = true;
                break;
            }
        }

        //if none of the predicates were true, try using the default value
        if (!resolved && Stringy.Relevant(defRef))
        {
            if (isStringLiteral(defRef!))
            {
                result = unwrapStringLiteral(defRef!);
            }
            else
            {
                result = ResolveModelReference(defRef!, model);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets info for a method with the given name and with a signature that indicates it can be used in binding
    /// (BindingModel bm) => object?
    /// </summary>
    /// <param name="method">Binding method reference string</param>
    /// <param name="host">Object to look for target method on</param>
    /// <returns>MethodInfo for the desired method, or null if method does not exist</returns>
    private static MethodInfo? getBindingMethodInfo(string method, object host)
    {
        MethodGuard.NoEmptyString(new { method });
        MethodGuard.NoNull(new { host });

        //attempt to get method info
        var t = host.GetType();
        try
        {
            return t.GetMethod(method, [typeof(BindingModel)]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a reference to a binding method by invoking it
    /// </summary>
    /// <param name="bindingMethod">Invocation object for binding method</param>
    /// <param name="host">Host object on which to invoke the binding method</param>
    /// <param name="model">Model to pass to the binding method</param>
    /// <returns></returns>
    private static object? resolveBindingMethod(MethodInfo bindingMethod,
        object host, BindingModel model)
    {
        MethodGuard.NoNull(new {bindingMethod, host, model });

        try
        {
            return bindingMethod.Invoke(host, [model]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the object and/or object member from the given model's context tree
    /// referred to by the given context tree reference string
    /// </summary>
    /// <param name="treeRef">Reference to context tree object / object member</param>
    /// <param name="model">Binding model being used to resolve context tree
    /// object reference</param>
    /// <returns></returns>
    private static object? ResolveContextTreeReference(string treeRef, BindingModel model)
    {
        MethodGuard.NoEmptyString(new { treeRef });
        MethodGuard.NoNull(new { model });

        //get base context object
        string key =
            Regex.Match(treeRef, "^@?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase).Value;
        object? property = null;
        if (model.ContextTree.ContainsKey(key)) { property = model.ContextTree[key]; }


        //resolve collection member references, if necessary
        if (property != null)
        {
            //get any indices referred to
            var indices = getCollectionIndices(treeRef);
            //resolve property value to member referred to in parent collection
            foreach (var idx in indices)
            {
                property = resolveIndexedProperty(property, idx);
                if (property == null) { break; }
            }
        }

        return property;
    }

    /// <summary>
    /// Determines if the given property reference string contains a reference to a
    /// specific context, or if the reference implies use of the model's current context
    /// </summary>
    /// <param name="propRef">The property reference string to evaluate</param>
    /// <param name="model">The binding model to use in making
    /// a determination</param>
    /// <returns>True if the given property reference contains an explicit
    /// reference to a specific context object, otherwise false</returns>
    private static bool containsContextReference(string propRef, BindingModel model)
    {
        MethodGuard.NoEmptyString(new { propRef });
        MethodGuard.NoNull(new { model });

        //get possible context reference
        string key =
            Regex.Match(propRef, @"^@?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase).Value;

        return model.ContextTree.ContainsKey(key);
    }

    /// <summary>
    /// Extracts all collection member index numbers from a property string
    /// </summary>
    /// <param name="property">Property to extract indices from</param>
    /// <returns>An array of all index numbers referenced in the given
    /// property</returns>
    private static int[] getCollectionIndices(string property)
    {
        //get index reference strings 
        var strIndices = Regex.Matches(property, @"\[[0-9]+\]").Cast<Match>()
            .Select(i => i.Value);

        //convert strings to a list of numbers
        return strIndices.Select(i =>
        {
            //strip square brackets
            string txtIndex = Regex.Match(i, "[0-9]+").Value;
            return Convert.ToInt32(txtIndex);
        }).ToArray();
    }

    /// <summary>
    /// Gets the nth member of a collection or enumerable object
    /// </summary>
    /// <param name="list">The collection or enumerable object</param>
    /// <param name="index">index of desired parent object member</param>
    /// <returns>The member of the given collection or enumerable referenced with the given
    /// index</returns>
    private static object? resolveIndexedProperty(object? list, int index)
    {
        if (list == null) { return null; }

        //object passed in is a list as expected
        if (typeof(IList).IsAssignableFrom(list.GetType()))
        {
            IList iList = (IList)list;
            //return null if index out of bounds
            if (index >= iList.Count) { return null; }

            return iList[index];
        }
        //handle string
        else if (list.GetType() == typeof(string))
        {
            string str = (string)list;
            //return null if index out of bounds
            if (index >= str.Length) { return null; }

            return str[index];
        }
        //handle any other type of enumerable
        else if (typeof(IEnumerable).IsAssignableFrom(list.GetType()))
        {
            IEnumerable iEnum = (IEnumerable)list;
            //cycle through to desired item
            int inc = 0;
            foreach (var item in iEnum)
            {
                if (inc == index)
                {
                    return item;
                }
                inc++;
            }
            //index was out of range...
            return null;
        }

        //object is not known to be indexable
        return null;
    }

    private const string BINDING_EXPRESSION_REGEX = @"\{\{\s*((@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)|((if(not)?\s*\(\s*(@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)\s*\,\s*((@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)|`[^`]*`)\s*\)\s*)+((@?[a-z_][a-z0-9_]*(\[[0-9]+\])*(\.@?[a-z_][a-z0-9_]*(\[[0-9]+\])*)*)|`[^`]*`)?))(\s*\|\s*([^\s`]*|`[^`]*`))?\s*\}\}";
}
