using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;

namespace Marta.Thermomix
{
    class CookingService
    {
        public static async Task<List<string>> GetRecipesIdsAsync(int page, HttpClient httpClient, string url)
        {
            string nextUrl = $"{url}?page={page}&limit=100";
            var recipes = await httpClient.GetAsync(nextUrl);

            if (!recipes.IsSuccessStatusCode)
            {
                throw new Exception("Cannot load recipes");
            }

            var jsonContent = await recipes.Content.ReadAsStringAsync();

            // todo to async
            dynamic deserializeObject = JsonConvert.DeserializeObject(jsonContent);
            var ids = new List<string>();
            foreach (dynamic c in deserializeObject.content)
            {
                string href = c.links[0].href;
                ids.Add(CookingService.ParseRecipeId(href));
            }

            return ids;
        }

        public static async Task<Recipe> GetRecipeDetailsAsync(string id, HttpClient httpClient, string url)
        {
            var details = await httpClient.GetAsync($"{url}/{id}");

            if (!details.IsSuccessStatusCode)
            {
                throw new Exception($"Cannot load recipe with {id} id");
            }

            var detailsRead = await details.Content.ReadAsStringAsync();
            dynamic deserializeObject = JsonConvert.DeserializeObject(detailsRead);
            dynamic content = deserializeObject;
            var recipe = new Recipe
            {
                Name = content.name,
                Id = id
            };

            foreach (dynamic ingredient in content.recipeIngredientGroups[0].recipeIngredients)
            {
                string name = ingredient.ingredient.name;
                recipe.Ingredients.Add(name);
            }

            return recipe;
        }
        public static string ParseRecipeId(string href)
        {
            var match = new Regex(@"/(?<id>[0-9]{4,})").Match(href);
            if (!match.Success)
            {
                throw new Exception($"No matched id {href}");
            }

            return match.Groups["id"].Value;
        }
    }
}