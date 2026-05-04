namespace OpticEMS.Contracts.Services.Database
{
    public interface IRecipeRepository
    {
        Task<List<Recipe.Recipe>> GetRecipesAsync();

        Task<Recipe.Recipe?> GetRecipeByRecipeIdAsync(int id, CancellationToken cancellationToken = default);

        Task AddRecipeAsync(Recipe.Recipe recipe, CancellationToken cancellationToken = default);

        Task UpdateRecipeAsync(Recipe.Recipe updatedRecipe, CancellationToken cancellationToken = default);

        void RemoveRecipe(Recipe.Recipe recipe);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
