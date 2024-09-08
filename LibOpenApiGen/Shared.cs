
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static LibOpenApiGen.ApiDocument;

namespace LibOpenApiGen
{
	public static class Shared
	{
		public static string ConvertToPascalCase(string str)
		{
			return Regex.Replace(str, @"(^\w|-\w)", (e) => e.Value.Replace("-", "").ToUpper());//Regex.Replace(str, @"\b\p{Ll}", match => match.Value.ToUpper());
		}

		public static Dictionary<string, Property> ReturnAllPropertiesDictionary(Schema schema, Dictionary<string, Schema> root)
		{
			var properties = new Dictionary<string, Property>();
			var components = schema.OneOf ?? schema.AllOf;
			foreach (var componentOf in components!)
			{
				if (componentOf.Ref == null) continue;
				var Ref = componentOf.Ref.Replace("#/components/schemas/", "");
				if (Ref == null) continue;
				if (root[Ref].Properties != null)
				{
					foreach (var property in root[Ref].Properties!)
					{
						properties[property.Key] = property.Value;
					}
				}
				else
				{
					var res = ReturnAllPropertiesDictionary(root[Ref], root);
					properties = properties.Concat(res.Where(pair =>
							!properties.ContainsKey(pair.Key))
						).ToDictionary(
							pair => pair.Key,
							pair => pair.Value
						);
				}
			}
			return properties;
		}
		
		public static Dictionary<string, Property> ReturnAllPropertiesDictionary(Schema schema,
																			Dictionary<string, Schema> root, out List<string> baseModels)
		{
			var properties = new Dictionary<string, Property>();
			var components = schema.OneOf ?? schema.AllOf;
			var models = new List<string>();
			foreach (var componentOf in components!)
			{
				if (componentOf.Ref == null) continue;
				var Ref = componentOf.Ref.Replace("#/components/schemas/", "");
				if (Ref == null) continue;
				models.Add(Ref);
				if (root[Ref].Properties != null)
				{
					foreach (var property in root[Ref].Properties!)
					{
						properties[property.Key] = property.Value;
					}
				}
				else
				{
					var res = ReturnAllPropertiesDictionary(root[Ref], root, out models);
					properties = properties.Concat(res.Where(pair =>
							!properties.ContainsKey(pair.Key))
						).ToDictionary(
							pair => pair.Key,
							pair => pair.Value
						);
				}
			}
			baseModels = models;
			return properties;
		}

		public static void GenerateToStringCode(StringBuilder sb, Dictionary<string, Property> properties, string model = "", int indent = 2)
		{
			var indentStr = new string('\t', indent);
			sb.Append($"{indentStr}public override string ToString()\n");
			sb.Append($"{indentStr}{{\n");
			sb.Append($"{indentStr}\tvar sb = new StringBuilder();\n");
			sb.Append($"{indentStr}\tsb.Append(\"");
			if (model != "")
			{
				sb.Append($"class {model}: ");
			}
			sb.Append($"{{\\n\");\n");
			foreach (var property in properties)
			{
				var type = "";
				if (property.Value.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
				{
					type = s;
				}
				else if (property.Value.Type is JsonArray ja)
				{
					type = ja[0]?.ToString() ?? "JsonNode";
				}
				if (type == "array")
				{
					sb.Append($"{indentStr}\tsb.Append(\"  {property.Key}: [\\n\");\n");
					if (property.Value.Items != null && property.Value.Items.Ref != null)
					{
						sb.Append($"{indentStr}\tif (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0)\n");
						sb.Append($"{indentStr}\t{{\n");
						sb.Append($"{indentStr}\t\tvar sb2 = new StringBuilder();\n");
						sb.Append($"{indentStr}\t\tsb2.Append(\"    \");\n");
						sb.Append($"{indentStr}\t\tthis.{ConvertToPascalCase(property.Key)}.ForEach(item =>\n");
						sb.Append($"{indentStr}\t\t{{\n");
						sb.Append($"{indentStr}\t\t\tsb2.Append(item).Append(\",\");\n");
						sb.Append($"{indentStr}\t\t\tif (item != this.{ConvertToPascalCase(property.Key)}.Last()) sb2.Append(\"\\n\");\n");
						sb.Append($"{indentStr}\t\t}});\n");
						sb.Append($"{indentStr}\t\tsb2.Replace(\"\\n\", \"\\n    \");\n");
						sb.Append($"{indentStr}\t\tsb2.Append(\"\\n\");\n");
						sb.Append($"{indentStr}\t\tsb.Append(sb2);\n");
						sb.Append($"{indentStr}\t}}\n");
					}
					else
					{
						sb.Append($"\t{indentStr}if (this.{ConvertToPascalCase(property.Key)} != null && this.{ConvertToPascalCase(property.Key)}.Count > 0)\n");
						sb.Append($"\t{indentStr}{{\n");
						sb.Append($"\t{indentStr}\tvar sb{ConvertToPascalCase(property.Key)} = new StringBuilder();\n");
						sb.Append($"\t{indentStr}\tsb{ConvertToPascalCase(property.Key)}.Append(\"    \");\n");
						sb.Append($"\t{indentStr}\tthis.{ConvertToPascalCase(property.Key)}.ForEach(item => sb{ConvertToPascalCase(property.Key)}.Append(item).Append(\",\\n\"));\n");
						sb.Append($"\t{indentStr}\tsb{ConvertToPascalCase(property.Key)}.Replace(\"\\n\", \"\\n    \");\n");
						sb.Append($"\t{indentStr}\tsb{ConvertToPascalCase(property.Key)}.Length -= 4;\n");
						sb.Append($"\t{indentStr}\tsb.Append(sb{ConvertToPascalCase(property.Key)});\n");
						sb.Append($"\t{indentStr}}}\n");
					}
					sb.Append($"{indentStr}\tsb.Append(\"  ]\\n\");\n");
				}
				else
				{
					if (property.Value.Ref != null || type == "object")
					{
						// sb.Append($"\t\t\tsb.Append(\"  {property.Key}: {{\\n\").Append(\"    \").Append(this.{Regex.Replace(property.Key, @"\b\p{Ll}", match => match.Value.ToUpper())}).Replace(\"\\n\", \"\\n    \").Append(\"\\n\").Append(\"  }}");
						sb.Append($"{indentStr}\tvar sb{ConvertToPascalCase(property.Key)} = new StringBuilder();\n");
						sb.Append($"{indentStr}\tsb{ConvertToPascalCase(property.Key)}.Append(\"  {property.Key}: [\\n\");\n");
						sb.Append($"{indentStr}\tif (this.{ConvertToPascalCase(property.Key)} != null)\n");
						sb.Append($"{indentStr}\t{{\n");
						sb.Append($"{indentStr}\t\tsb{ConvertToPascalCase(property.Key)}.Append(this.{ConvertToPascalCase(property.Key)});\n");
						sb.Append($"{indentStr}\t\tsb{ConvertToPascalCase(property.Key)}.Replace(\"\\n\", \"\\n    \");\n");
						sb.Append($"{indentStr}\t\tsb{ConvertToPascalCase(property.Key)}.Append(\"\\n\");\n");
						sb.Append($"{indentStr}\t}}\n");
						sb.Append($"{indentStr}\tsb{ConvertToPascalCase(property.Key)}.Append(\"  ]\\n\");\n");
						sb.Append($"{indentStr}\tsb.Append(sb{ConvertToPascalCase(property.Key)});\n");
					}
					else
					{
						sb.Append($"{indentStr}\tsb.Append($\"  {property.Key}: {{this.{ConvertToPascalCase(property.Key)}}}\\n\");\n");
					}
				}
			}
			sb.Append($"{indentStr}\tsb.Append(\"}}\");\n");
			sb.Append($"{indentStr}\treturn sb.ToString();\n");
			sb.Append($"{indentStr}}}\n");
		}

		public class PropertyType
		{
			public string Type { get; set; }
			public bool Nullable { get; set; }

			public PropertyType(string type, bool nullable)
			{
				this.Type = type;
				this.Nullable = nullable;
			}
		}

		public static string ParseAdditionalProperties(JsonNode? additionalProperties)
		{
			if (additionalProperties == null) return "";
			if (additionalProperties is JsonValue jv && jv.AsValue().TryGetValue<bool>(out var b))
			{
				if (b)
				{
					return "Dictionary<string, JsonNode>";
				}
			}
			else if (additionalProperties["anyOf"] != null &&
				additionalProperties["anyOf"] is JsonArray ja &&
				ja.Count == 1)
			{
				var additionalPropertiesProperty = JsonSerializer.Deserialize<Property>(ja[0]?.ToString() ?? "");
				if (additionalPropertiesProperty != null)
				{
					var dicType = additionalPropertiesProperty.Type?.ToString() ?? "";
					if (dicType == "number") dicType = "decimal";
					if (dicType == "integer") dicType = "int";
					if (dicType == "boolean") dicType = "bool";
					if (dicType == "object")
					{
						return "Dictionary<string, JsonNode>";
					}
					return $"Dictionary<string, {dicType}>";
				}
			}
			else if (additionalProperties["type"] != null &&
				additionalProperties["type"] is JsonValue jv2 &&
				jv2.AsValue().TryGetValue<string>(out var s))
			{
				if (s == "object")
				{
					return "Dictionary<string, JsonNode>";
				}
				return $"Dictionary<string, {s}>";
			}
			return "";
		}

		public static PropertyType GetPropertyType(Property property, StringBuilder sb,
																								string name, string model, int indent = 2)
		{
			var nullable = false;
			var indentStr = new string('\t', indent);
			string? type;
			if (property.Type is JsonValue jv && jv.AsValue().TryGetValue<string>(out var s))
			{
					type = s;
			}
			else if (property.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
			{
					nullable = true;
					type = ja[0]?.ToString() ?? "JsonNode";
			}
			else
			{
					type = property.Type?.ToString() ?? "";
			}
			if (property.Ref != null)
			{
				var Ref = property.Ref.Replace("#/components/schemas/", "");
				type = $"{Ref}";
			}
			else
			{
				type = $"{type.ToLower()}";
			}
			if (type == "object")
			{
				type = "JsonNode";
				if (property.Properties != null)
				{
					type = $"{model}{name}Object";
					sb.Append($"{indentStr}public class {type} {{\n");
					GeneratePropertiesCode(sb, property.Properties, model, indent + 1);
					GenerateToStringCode(sb, property.Properties, type, indent + 1);
					sb.Append($"{indentStr}}}\n");
				}
				else if (property.AllOf != null)
				{
					if (property.AllOf.Count == 1)
					{
						type = property.AllOf[0]["ref"].Replace("#/components/schemas/", "");
					}
				}
				else if (property.AdditionalProperties != null)
				{
					type = ParseAdditionalProperties(property.AdditionalProperties);
				}
			}
			if (type == "array")
			{
				var listType = property.UniqueItems != null && property.UniqueItems == true ? "UniqueList" : "List";
				type = $"{listType}<JsonNode>";
				if (property.Items != null)
				{
					if (property.Items.Type != null)
					{
						string itemsType = "";
						bool nullableItems = false;
						if (property.Items.Type is JsonValue jv2 && jv2.AsValue().TryGetValue<string>(out var s2))
						{
							itemsType = s2;
						}
						else if (property.Items.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
						{
							itemsType = ja[0]?.ToString() ?? "JsonNode";
							nullableItems = true;
						}
						type = $"{listType}<{itemsType}>";
						if (property.Items != null)
						{
							if (itemsType == "JsonNode")
							{
								string className = $"{model}{name}ItemType";
								sb.Append($"{indentStr}public class {className} {{\n");
								GeneratePropertiesCode(sb, new Dictionary<string, Property>() { { name, property.Items } }, className, indent + 1);
								GenerateToStringCode(sb, new Dictionary<string, Property>() { { name, property.Items } }, className, indent + 1);
								sb.Append($"{indentStr}}}\n");
								type = $"{listType}<{name}ItemType>";
							}
							if (property.Items.Ref != null)
							{
								var Ref = property.Items.Ref.Replace("#/components/schemas/", "");
								type = $"{listType}<{Ref}>";
							}
							// リストでオブジェクトがある場合はクラスを生成
							if (itemsType == "object" && property.Items.Properties != null)
							{
								sb.Append($"{indentStr}public class {model}{name}PropertyType {{\n");
								GeneratePropertiesCode(sb, property.Items.Properties, model, indent + 1);
								GenerateToStringCode(sb, property.Items.Properties, $"{model}{name}PropertyType", indent + 1);
								sb.Append($"{indentStr}}}\n");
								type = $"{listType}<{model}{name}PropertyType>";
							}
							if (nullableItems)
							{
								type += "?";
							}
							// リストのリスト用
							if (property.Items.Items != null && property.Items.Items.Type != null)
							{
								string itemsItemType = "";
								bool nullableItemsItems = false;
								if (property.Items.Type is JsonValue jv3 && jv3.AsValue().TryGetValue<string>(out var s3))
								{
									itemsItemType = s3;
								}
								else if (property.Items.Type is JsonArray ja && ja != null && ja.Count > 0 && ja[0] != null)
								{
									itemsItemType = ja[0]?.ToString() ?? "JsonNode";
									nullableItemsItems = true;
								}
								type = $"{listType}<List<{itemsItemType}>>";
								if (property.Items.Items.Ref != null)
								{
									var Ref = property.Items.Items.Ref.Replace("#/components/schemas/", "");
									type = $"{listType}<List<{Ref}>>";
								}
								else if (property.Items.Items != null && property.Items.Items.Type != null)
								{
									type = $"{listType}<List<{property.Items.Items.Type}>>";
								}
								if (nullableItemsItems)
								{
									type += "?";
								}
							}
						}
					}
				}
			}
			if (property.Format == "date-time") type = "DateTime";
			if (type == "number") type = "decimal";
			if (type == "integer") type = "int";
			if (type == "boolean") type = "bool";
			return new PropertyType(type, nullable);
		}

		public static void GeneratePropertiesCode(StringBuilder sb, Dictionary<string, Property> properties, string model, int indent = 2)
		{
			string name;
			string type;
			var indentStr = new string('\t', indent);
			foreach (var property in properties)
			{
				name = ConvertToPascalCase(property.Key);
				if (Regex.IsMatch(name, "^[0-9]")) name = "_" + name;
				var propertyType = GetPropertyType(property.Value, sb, name, model, indent);
				type = propertyType.Type;
				if (propertyType.Nullable) type += "?";
				sb.Append($"{indentStr}public {type} {name} {{ get; set; }}\n");
			}
		}
	}
}