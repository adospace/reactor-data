﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;


namespace ReactorData;

[Generator]
public class ModelPartialClassSourceGenerator : ISourceGenerator
{

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization((i) => i.AddSource("ModelAttribute.g.cs", @"using System;

#nullable enable
namespace ReactorData
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    class ModelAttribute : Attribute
    {
    }


    [AttributeUsage(AttributeTargets.Property)]
    class ModelKeyAttribute : Attribute
    {
    }
}
"));

        context.RegisterForSyntaxNotifications(() => new ModelPartialClassSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        SymbolDisplayFormat qualifiedFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
        );

        var receiver = (ModelPartialClassSyntaxReceiver)context.SyntaxReceiver.EnsureNotNull();
        
        foreach (var modelToGenerate in receiver.ModelsToGenerate)
        {
            var semanticModel = context.Compilation.GetSemanticModel(modelToGenerate.SyntaxTree);

            // Get the class symbol from the semantic model
            var classTypeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(modelToGenerate).EnsureNotNull();

            string fullyQualifiedTypeName = classTypeSymbol.ToDisplayString(qualifiedFormat);
            string namespaceName = classTypeSymbol.ContainingNamespace.ToDisplayString();
            string className = classTypeSymbol.Name;

            string idPropertyName = "Id";

            string generatedSource = $$"""
                using System;

                #nullable enable

                namespace {{namespaceName}}
                {
                    partial class {{className}} : IEntity
                    {
                        object? IEntity.GetKey => {{idPropertyName}} == default ? null : {{idPropertyName}};
                    }
                }
                """;

            context.AddSource($"{className}.g.cs", generatedSource);
        }


        //// Format it to get the fully qualified name (namespace + type name).
        //SymbolDisplayFormat qualifiedFormat = new SymbolDisplayFormat(
        //    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
        //);
        //SymbolDisplayFormat symbolDisplayFormat = new SymbolDisplayFormat(
        //    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        //    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        //    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        //);

        //Dictionary<string, GeneratorClassItem> generatingClassItems = new();

        //void generateClassItem(FieldDeclarationSyntax fieldDeclaration, FieldAttributeType attributeType)
        //{
        //    // Get the semantic model for the syntax tree that has your field.
        //    var semanticModel = context.Compilation.GetSemanticModel(fieldDeclaration.SyntaxTree);

        //    // Get the TypeSyntax from the FieldDeclarationSyntax.
        //    TypeSyntax typeSyntax = fieldDeclaration.Declaration.Type;

        //    // Check if it is a nullable value type.
        //    bool isNullableValueType = typeSyntax is NullableTypeSyntax;

        //    // Get the type symbol using the semantic model.
        //    var fieldTypeSymbol = semanticModel.GetTypeInfo(typeSyntax).Type;

        //    if (fieldTypeSymbol == null)
        //    {
        //        return;
        //    }

        //    // Start with the fully qualified name of the type.
        //    string fieldTypeFullyQualifiedName = fieldTypeSymbol.ToDisplayString(symbolDisplayFormat);

        //    // Append "?" if it's a nullable value type or if the nullable annotation is set for reference types.
        //    if ((isNullableValueType || fieldTypeSymbol.NullableAnnotation == NullableAnnotation.Annotated) &&
        //        !fieldTypeFullyQualifiedName.EndsWith("?"))
        //    {
        //        fieldTypeFullyQualifiedName += "?";
        //    }

        //    if (fieldDeclaration.Declaration.Variables.Count != 1)
        //    {
        //        return;
        //    }

        //    var typeDeclarationSyntax = fieldDeclaration.Ancestors()
        //                        .OfType<TypeDeclarationSyntax>()
        //                        .FirstOrDefault();

        //    if (typeDeclarationSyntax == null)
        //    {
        //        return;
        //    }

        //    // Get the type symbol for the containing type.
        //    var classTypeSymbol = semanticModel.GetDeclaredSymbol(typeDeclarationSyntax);

        //    if (classTypeSymbol == null)
        //    {
        //        return;
        //    }

        //    string fullyQualifiedTypeName = classTypeSymbol.ToDisplayString(qualifiedFormat);
        //    string namespaceName = classTypeSymbol.ContainingNamespace.ToDisplayString();
        //    string className = classTypeSymbol.Name;

        //    if (!generatingClassItems.TryGetValue(fullyQualifiedTypeName, out var generatingClassItem))
        //    {
        //        generatingClassItems[fullyQualifiedTypeName] = generatingClassItem = new GeneratorClassItem(namespaceName, className);
        //    }

        //    foreach (var variableFieldSyntax in fieldDeclaration.Declaration.Variables)
        //    {
        //        var variableFieldName = variableFieldSyntax.Identifier.ValueText;

        //        if (generatingClassItem.FieldItems.ContainsKey(variableFieldName))
        //        {
        //            return;
        //        }

        //        string? methodName = null;
        //        if (attributeType == FieldAttributeType.Prop)
        //        {
        //            if (semanticModel.GetDeclaredSymbol(variableFieldSyntax) 
        //                is IFieldSymbol variableDeclaratorFieldSymbol)
        //            {
        //                var propAttributeData = variableDeclaratorFieldSymbol.GetAttributes()
        //                    .FirstOrDefault(_ => _.AttributeClass?.Name == "PropAttribute" || _.AttributeClass?.Name == "Prop");

        //                if (propAttributeData?.ConstructorArguments.Length > 0)
        //                {
        //                    methodName = propAttributeData.ConstructorArguments[0].Value?.ToString();
        //                }
        //            }
        //        }

        //        generatingClassItem.FieldItems[variableFieldName]
        //            = new GeneratorFieldItem(variableFieldName, fieldTypeFullyQualifiedName, attributeType, methodName);
        //    }
        //}

        //foreach (var injectFieldToGenerate in ((ModelPartialClassSyntaxReceiver)context.SyntaxReceiver.EnsureNotNull()).InjectFieldsToGenerate)
        //{
        //    generateClassItem(injectFieldToGenerate, FieldAttributeType.Inject);
        //}

        //foreach (var propFieldToGenerate in ((ModelPartialClassSyntaxReceiver)context.SyntaxReceiver.EnsureNotNull()).PropFieldsToGenerate)
        //{
        //    generateClassItem(propFieldToGenerate, FieldAttributeType.Prop);
        //}

        //foreach (var generatingClassItem in generatingClassItems.OrderBy(_=>_.Key)) 
        //{
        //    var textGenerator = new ComponentPartialClassGenerator(generatingClassItem.Value);

        //    var source = textGenerator.TransformAndPrettify();

        //    context.AddSource($"{generatingClassItem.Value.ClassName}.g.cs", source);
        //}   
    }
}

class ModelPartialClassSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> ModelsToGenerate = new();
    public List<PropertyDeclarationSyntax> ModelKeysToGenerate = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax cds)
        {
            var scaffoldAttribute = cds.AttributeLists
                .Where(_ => _.Attributes.Any(attr => attr.Name is IdentifierNameSyntax nameSyntax && 
                    (nameSyntax.Identifier.Text == "Model" || nameSyntax.Identifier.Text == "ModelAttribute")))
                .Select(_ => _.Attributes.First())
                .FirstOrDefault();

            if (scaffoldAttribute != null)
            {
                ModelsToGenerate.Add(cds);
            }
        }
    }
}

public class GeneratorClassItem
{
    public GeneratorClassItem(string @namespace, string className)
    {
        Namespace = @namespace;
        ClassName = className;
    }

    public string Namespace { get; }
    public string ClassName { get; }

    public Dictionary<string, GeneratorFieldItem> FieldItems { get; } = new();
}

public class GeneratorFieldItem
{
    private readonly string? _propMethodName;

    public GeneratorFieldItem(string fieldName, string fieldTypeFullyQualifiedName, FieldAttributeType type, string? propMethodName)
    {
        FieldName = fieldName;
        FieldTypeFullyQualifiedName = fieldTypeFullyQualifiedName;
        Type = type;
        _propMethodName = propMethodName;
    }

    public string FieldName { get; }

    public string FieldTypeFullyQualifiedName { get; }

    public FieldAttributeType Type { get; }

    public string GetPropMethodName()
    {
        if (_propMethodName != null)
        {
            return _propMethodName;
        }

        var fieldName = FieldName.TrimStart('_');
        fieldName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
        return fieldName;
    }
}

public enum FieldAttributeType
{
    Inject,

    Prop
}

