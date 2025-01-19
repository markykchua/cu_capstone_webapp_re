using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UserReplay
{
    public class JsonBinding
    {
        public ContentType OriginalType { get; set; }
        public JToken Value { get; set; }
        public JsonBinding(JToken value, ContentType originalType)
        {
            OriginalType = originalType;
            if (value is null || value.Type is JTokenType.Null || string.IsNullOrEmpty(value.ToString()))
            {
                Value = JValue.CreateNull();
            }
            else
            {
                try
                {
                    Value = OriginalType switch
                    {
                        ContentType.JSON => value.Type is JTokenType.String ? TryParse(value.Value<string>()) : value,
                        ContentType.XML => JObject.Parse(JsonConvert.SerializeXNode(XDocument.Parse(value.Value<string>()))),
                        ContentType.URL_FORM_ENCODED => Utils.FromUrlFormEncoded(value.Value<string>()),
                        ContentType.TEXT => value.Type is JTokenType.String ? value : value.ToString(),
                        _ => throw new ArgumentOutOfRangeException(nameof(originalType), $"Not expected content type value: {originalType}")
                    };
                }
                catch (Exception)
                {
                }
            }
        }
        public static JToken TryParse(string value)
        {
            try
            {
                return JToken.Parse(value);
            }
            catch (JsonReaderException)
            {
                return value;
            }
        }

        public JToken Original()
        {
            return OriginalType switch
            {
                ContentType.JSON => Value,
                ContentType.TEXT => Value.ToString(),
                ContentType.XML => (JValue)JsonConvert.DeserializeXNode(Value.ToString()).ToString(),
                ContentType.URL_FORM_ENCODED => Utils.ToUrlFormEncoded(Value as JObject),
                _ => throw new ArgumentOutOfRangeException(nameof(OriginalType), $"Not expected content type value: {OriginalType}")
            };
        }
    }

    public enum ContentType
    {
        JSON,
        XML,
        URL_FORM_ENCODED,
        TEXT
    }
}