using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Siberia.SourceLibrary.Finders;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Siberia.SourceLibrary.Generators
{
    [Generator]
    public class EntityDataModelGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DbContextFinder());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            Dictionary<(string, string), SyntaxList<MemberDeclarationSyntax>> contextMembers = new(); // Store DbContext childs with their members
            var contextList = ((DbContextFinder)context.SyntaxReceiver)?.ContextList; // Get classes that inherits DbContext

            foreach (var contextItem in contextList) // Iterate over each class that inherits DbContext
            {
                var contextNamespace = NamespaceFinder.GetNamespace(contextItem); // Namespace of the class
                var contextId = contextItem.Identifier.ValueText; // Name of the class
                var contextTuple = (contextId, contextNamespace); // Identifier of the class
                if (contextMembers.ContainsKey(contextTuple)) // Check if partial class is already present
                {
                    contextMembers[contextTuple] = contextMembers[contextTuple]
                        .AddRange(contextItem.Members.AsEnumerable()); // Add class members
                }
                else { contextMembers.Add(contextTuple, contextItem.Members); } // Add class members also
            }

            foreach (var contextMember in contextMembers) // Iterate over each unique context
            {
                string entitySet = ""; // Stores all entity set declarations
                string contextName = contextMember.Key.Item1; // Get context name
                foreach (var member in contextMember.Value) // Iterate over each context class member
                {
                    if (member is PropertyDeclarationSyntax property && property.Type.ToString().Contains("DbSet")) // Test if member is a DbSet
                    {
                        string typeName = property.Identifier.ValueText; // Get property name
                        string typeClass = property.Type.ToString().Replace("DbSet<", "").Replace(">", "").Replace("?", ""); // Get property type
                        entitySet += "            " + $@"builder.EntitySet<" + typeClass + ">(\"" + typeName + "\");" + Environment.NewLine; // Entity set declaration
                    }
                }

                string source = $@"// Auto-generated code
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using " + contextMember.Key.Item2 + ";" + Environment.NewLine +
"namespace " + context.Compilation.AssemblyName + ".Models.EntityDataModel" + $@"
{{
    public class " + contextName.Replace("Context", "") + $@"EntityDataModel
    {{
        public static IEdmModel GeEntityTypeDataModel()
        {{
            ODataConventionModelBuilder builder = new();" + Environment.NewLine + entitySet + $@"            return builder.GetEdmModel();
        }}
    }}
}}";

                context.AddSource(contextName.Replace("Context", "") + "EntityDataModel.g.cs", SourceText.From(source, Encoding.UTF8)); // Create new entity data model class
            }
        }
    }
}
