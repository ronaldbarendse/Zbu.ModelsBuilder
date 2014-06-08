﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Zbu.ModelsBuilder
{
    public class TextBuilder : Builder
    {
        public TextBuilder(IList<TypeModel> typeModels)
            : base(typeModels)
        { }

        public void Generate(StringBuilder sb, TypeModel typeModel)
        {
            WriteHeader(sb);

            foreach (var t in TypesUsing)
                sb.AppendFormat("using {0};\n", t);

            sb.Append("\n");
            sb.AppendFormat("namespace {0}\n", Disco.GetModelsNamespace(Namespace));
            sb.Append("{\n");

            WriteContentType(sb, typeModel);

            sb.Append("}\n");
        }

        public void WriteHeader(StringBuilder sb)
        {
            sb.Append("//------------------------------------------------------------------------------\n");
            sb.Append("// <auto-generated>\n");
            sb.Append("//   This code was generated by a tool.\n");
            sb.Append("//\n");
            sb.AppendFormat("//    Zbu.ModelsBuilder v{0}\n", Version);
            sb.Append("//\n");
            sb.Append("//   Changes to this file will be lost if the code is regenerated.\n");
            sb.Append("// </auto-generated>\n");
            sb.Append("//------------------------------------------------------------------------------\n");
            sb.Append("\n");
        }

        void WriteContentType(StringBuilder sb, TypeModel type)
        {
            string sep;

            if (type.IsMixin)
            {
                // write the interface declaration
                sb.AppendFormat("\t// Mixin content Type {0} with alias \"{1}\"\n", type.Id, type.Alias);
                sb.AppendFormat("\tpublic partial interface I{0}", type.Name);
                var implements = type.BaseType == null || type.BaseType.IsContentIgnored
                    ? (type.HasBase ? null : "PublishedContent") 
                    : type.BaseType.Name;
                if (implements != null)
                    sb.AppendFormat(" : I{0}", implements);

                // write the mixins
                sep = implements == null ? ":" : ",";
                foreach (var mixinType in type.DeclaringInterfaces.OrderBy(x => x.Name))
                {
                    sb.AppendFormat("{0} I{1}", sep, mixinType.Name);
                    sep = ",";
                }

                sb.Append("\n\t{\n");

                // write the properties - only the local ones, we're an interface
                var more = false;
                foreach (var prop in type.Properties.OrderBy(x => x.Name))
                {
                    if (more) sb.Append("\n");
                    more = true;
                    WriteInterfaceProperty(sb, prop);
                }

                sb.Append("\t}\n\n");
            }

            // write the class declaration
            if (type.IsRenamed)
            sb.AppendFormat("\t// Content Type {0} with alias \"{1}\"\n", type.Id, type.Alias);
            sb.AppendFormat("\t[PublishedContentModel(\"{0}\")]\n", type.Alias);
            sb.AppendFormat("\tpublic partial class {0}", type.Name);
            var inherits = type.BaseType == null || type.BaseType.IsContentIgnored
                ? (type.HasBase ? null : Disco.GetModelsBaseClassName("PublishedContentModel"))
                : type.BaseType.Name;
            if (inherits != null)
                sb.AppendFormat(" : {0}", inherits);

            sep = inherits == null ? ":" : ",";
            if (type.IsMixin)
            {
                // if it's a mixin it implements its own interface
                sb.AppendFormat("{0} I{1}", sep, type.Name);
            }
            else
            {
                // write the mixins, if any, as interfaces
                // only if not a mixin because otherwise the interface already has them already
                foreach (var mixinType in type.DeclaringInterfaces.OrderBy(x => x.Name))
                {
                    sb.AppendFormat("{0} I{1}", sep, mixinType.Name);
                    sep = ",";
                }
            }

            // begin class body
            sb.Append("\n\t{\n");

            // write the constants
            // as 'new' since parent has its own
            sb.AppendFormat("\t\tpublic new const string ModelTypeAlias = \"{0}\";\n",
                type.Alias);
            sb.AppendFormat("\t\tpublic new const PublishedItemType ModelItemType = PublishedItemType.{0};\n\n",
                type.ItemType);

            // write the ctor
            sb.AppendFormat("\t\tpublic {0}(IPublishedContent content)\n\t\t\t: base(content)\n\t\t{{ }}\n\n",
                type.Name);

            // write the static methods
            // as 'new' since parent has its own
            sb.Append("\t\tpublic new static PublishedContentType GetModelContentType()\n");
            sb.Append("\t\t{\n\t\t\treturn PublishedContentType.Get(ModelItemType, ModelTypeAlias);\n\t\t}\n\n");
            sb.AppendFormat("\t\tpublic static PublishedPropertyType GetModelPropertyType<TValue>(Expression<Func<{0}, TValue>> selector)\n",
                type.Name);
            sb.Append("\t\t{\n\t\t\treturn PublishedContentModelUtility.GetModelPropertyType(GetModelContentType(), selector);\n\t\t}\n");

            // write the properties
            WriteContentTypeProperties(sb, type);

            // close the class declaration
            sb.Append("\t}\n");
        }

        void WriteContentTypeProperties(StringBuilder sb, TypeModel type)
        {
            // write the properties
            foreach (var prop in type.Properties.Where(x => !x.IsIgnored).OrderBy(x => x.Name))
                WriteProperty(sb, prop);

            // no need to write the parent properties since we inherit from the parent
            // and the parent defines its own properties. need to write the mixins properties
            // since the mixins are only interfaces and we have to provide an implementation.

            // write the mixins properties
            foreach (var mixinType in type.ImplementingInterfaces.OrderBy(x => x.Name))
                foreach (var prop in mixinType.Properties.Where(x => !x.IsIgnored).OrderBy(x => x.Name))
                    WriteProperty(sb, prop);
        }

        void WriteProperty(StringBuilder sb, PropertyModel property)
        {
            sb.Append("\n");

            sb.AppendFormat("\t\t[ModelPropertyAlias(\"{0}\")]\n", property.Alias);

            sb.Append("\t\tpublic ");
            WriteClrType(sb, property.ClrType);
            sb.AppendFormat(" {0}\n\t\t{{\n\t\t\tget {{ return this.GetPropertyValue",
                property.Name);
            if (property.ClrType != typeof(object))
            {
                sb.Append("<");
                WriteClrType(sb, property.ClrType);
                sb.Append(">");
            }
            sb.AppendFormat("(\"{0}\"); }}\n\t\t}}\n",
                property.Alias);
        }

        void WriteInterfaceProperty(StringBuilder sb, PropertyModel property)
        {
            sb.Append("\t\t");
            WriteClrType(sb, property.ClrType);
            sb.AppendFormat(" {0} {{ get; }}\n",
                property.Name);
        }

        internal void WriteClrType(StringBuilder sb, Type type)
        {
            var s = type.ToString();

            if (type.IsGenericType)
            {
                var p = s.IndexOf('`');
                WriteNonGenericClrType(sb, s.Substring(0, p));
                sb.Append("<");
                var args = type.GetGenericArguments();
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    WriteClrType(sb, args[i]);
                }
                sb.Append(">");
            }
            else
            {
                WriteNonGenericClrType(sb, s);
            }
        }

        void WriteNonGenericClrType(StringBuilder sb, string s)
        {
            var ls = s.ToLowerInvariant();
            if (_typesMap.ContainsKey(ls))
            {
                s = _typesMap[ls];
            }
            else
            {
                var p = s.LastIndexOf('.');
                if (p > 0 && TypesUsing.Contains(s.Substring(0, p)))
                    s = s.Substring(p + 1);
                s = s.Replace("+", "."); // nested types *after* using
            }

            sb.Append(s);
        }

        private readonly IDictionary<string, string> _typesMap = new Dictionary<string, string>
        {
            { "system.int16", "short" },
            { "system.int32", "int" },
            { "system.int64", "long" },
            { "system.string", "string" },
            { "system.object", "object" },
            { "system.boolean", "bool" },
            { "system.void", "void" },
            { "system.char", "char" },
            { "system.byte", "byte" },
            { "system.uint16", "ushort" },
            { "system.uint32", "uint" },
            { "system.uint64", "ulong" },
            { "system.sbyte", "sbyte" },
            { "system.single", "float" },
            { "system.double", "double" },
            { "system.decimal", "decimal" }
        };
    }
}
