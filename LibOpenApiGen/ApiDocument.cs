using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LibOpenApiGen
{
    public class ApiDocument
    {
        public enum HttpMethod
        {
            Get,
            Post,
            Put,
            Patch,
            Delete,
        }

        public static string ConvertToPascalCase(string str)
        {
            return Regex.Replace(str, @"\b\p{Ll}", match => match.Value.ToUpper());
        }

        public string Openapi { get; set; }
        public Dictionary<string, Dictionary<HttpMethod, PathsMethod>> Paths { get; set; }
        public Dictionary<string, Dictionary<string, ComponentsMethod>> Components { get; set; }
        public class Property
        {
            public JsonNode Type { get; set; }
            public string? Description { get; set; }
            public JsonNode? Properties { get; set; }
            public object? Default { get; set; }
            public string? Format { get; set; }
            public JsonNode? Example { get; set; }
            public string[]? Enum { get; set; }
            public Property? Items { get; set; }
            public string? Ref { get; set; }
        }
        public class PathsMethod
        {
            public class Schema
            {
                public object Type { get; set; }
                public Dictionary<string, Property>? Properties { get; set; }
                public string? Ref { get; set; }
                public string[]? Required { get; set; }
                public JsonNode[]? AnyOf { get; set; }
                public Property[]? OneOf { get; set; }
                public Property? Items { get; set; }
            }

            public class ContentType
            {
                public Schema Schema { get; set; }
            }

            public class RequestBodyClass
            {
                public bool Required { get; set; }
                public Dictionary<string, ContentType> Content { get; set; }
            }

            public class ResponsesClass
            {
                public string Description { get; set; }
                public Dictionary<string, ContentType> Content { get; set; }
            }

            public string OperationId { get; set; }
            public string Summary { get; set; }
            public string Description { get; set; }
            public JsonNode? Security { get; set; }
            public RequestBodyClass RequestBody { get; set; }
            public Dictionary<int, ResponsesClass> Responses { get; set; }
        }

        public class ComponentsMethod
        {
            public string Type { get; set; }
            public Dictionary<string, Property>? Properties { get; set; }
            public string[]? Required { get; set; }
            public Property[]? OneOf { get; set; }
            public Property[]? AllOf { get; set; }
        }
    }

}
