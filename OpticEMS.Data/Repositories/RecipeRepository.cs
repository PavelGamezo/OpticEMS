using Microsoft.EntityFrameworkCore;
using OpticEMS.Contracts.Services.Database;
using OpticEMS.Contracts.Services.Recipe;
using OpticEMS.Data.Database.Context;

namespace OpticEMS.Data.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly RecipeDbContext _context;

        public RecipeRepository(RecipeDbContext context)
        {
            _context = context;
        }

        public async Task AddRecipeAsync(Recipe recipe, CancellationToken cancellationToken = default)
        {
            await _context.Recipes.AddAsync(recipe, cancellationToken);
        }

        public async Task<List<Recipe>> GetRecipesAsync()
        {
            var recipes = await _context.Recipes.ToListAsync();

            return recipes;
        }

        public async Task<Recipe?> GetRecipeByRecipeIdAsync(int recipeId, CancellationToken cancellationToken = default)
        {
            var recipe = await _context.Recipes
                .FirstOrDefaultAsync(
                    recipe => recipe.RecipeId == recipeId,
                    cancellationToken);

            return recipe;
        }

        public void RemoveRecipe(Recipe recipe)
        {
            _context.Recipes.Remove(recipe);
        }

        public async Task UpdateRecipeAsync(Recipe updatedRecipe, CancellationToken cancellationToken = default)
        {
            var existingRecipe = await _context.Recipes.FindAsync(updatedRecipe.DatabaseId, cancellationToken);

            if (existingRecipe != null)
            {
                _context.Entry(existingRecipe).CurrentValues.SetValues(updatedRecipe);

                existingRecipe.Wavelengths = updatedRecipe.Wavelengths.ToList();
                existingRecipe.WavelengthColors = updatedRecipe.WavelengthColors.ToList();
                existingRecipe.WavelengthNames = updatedRecipe.WavelengthNames.ToList();

                existingRecipe.LastModifiedAt = DateTime.Now;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
