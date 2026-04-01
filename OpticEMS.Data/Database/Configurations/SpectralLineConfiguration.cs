using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticEMS.Contracts.Services.Database;

namespace OpticEMS.Data.Database.Configurations
{
    public class SpectralLineConfiguration : IEntityTypeConfiguration<SpectralLine>
    {
        public void Configure(EntityTypeBuilder<SpectralLine> builder)
        {
            builder.HasIndex(x => x.Wavelength);
        }
    }
}
