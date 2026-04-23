namespace OpticEMS.Contracts.Services.Recipe
{
    public interface IRecipeFileManager
    {
        Task SaveRecipe(Recipe recipe);

        Task<List<Recipe>> LoadRecipeFiles();

        Task RenameRecipe(string oldName, Recipe recipe);

        void DeleteRecipe(string name);
    }
}
