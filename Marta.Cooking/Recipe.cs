using Newtonsoft.Json;
using System.Collections.Generic;

namespace Marta.Thermomix
{
    class Recipe
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "ingredients")]
        public List<string> Ingredients { get; set; } = new List<string>();
    }
}