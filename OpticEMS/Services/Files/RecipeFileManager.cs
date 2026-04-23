using OpticEMS.Contracts.Services.Recipe;
using System.IO;
using System.Text.Json;

namespace OpticEMS.Services.Files
{
    public class RecipeFileManager : IRecipeFileManager
    {
        private const string folderPath = @"D:\Design";

        public void DeleteRecipe(string name)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, name + ".json");

            if (!File.Exists(filePath))
            {
                throw new Exception("File was not found");
            }

            File.Delete(filePath);
        }

        public async Task<List<Recipe>> LoadRecipeFiles()
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var recipes = new List<Recipe>();
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            foreach (var file in Directory.GetFiles(folderPath, "*.json"))
            {
                using (FileStream createStream = File.OpenRead(file))
                {
                    try
                    {
                        var recipe = await JsonSerializer.DeserializeAsync<Recipe>(createStream);

                        recipes.Add(recipe);
                    }
                    catch
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);

                        recipes.Add(new Recipe()
                        {
                            Name = fileName
                        });
                    }
                }
            }

            return recipes;
        }

        public async Task RenameRecipe(string oldName, Recipe recipe)
        {
            var oldPath = Path.Combine(folderPath, oldName + ".json");
            var newPath = Path.Combine(folderPath, recipe.Name + ".json");

            if (!File.Exists(oldPath))
            {
                throw new FileNotFoundException($"File not found: {oldPath}");
            }

            using (FileStream renameStream = File.OpenWrite(oldPath))
            {
                await JsonSerializer.SerializeAsync(renameStream, recipe);
            }

            File.Move(oldPath, newPath);
        }

        public async Task SaveRecipe(Recipe recipe)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, $"{recipe.Name}.json");
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, recipe, options);
                }
            }
            catch (Exception exception)
            {
                throw new IOException($"Can't save recipe into the file: {filePath}", exception);
            }
        }
    }
}
