using OpticEMS.MVVM.Models;

namespace OpticEMS.Services.Files
{
    public interface IRecipeFileManager
    {
        Task SaveRecipe(RecipeModel recipe);

        Task<List<RecipeModel>> LoadRecipeFiles();

        Task RenameRecipe(string oldName, RecipeModel recipe);

        void DeleteRecipe(string name);
    }
}
