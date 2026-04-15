using OpticEMS.License.Handlers;

namespace OpticEMS.License.Common
{
    public sealed class OpticEMSLicense : License
    {
        public override LicenseStatus DoExtraValidation(out string validationMsg)
        {
            LicenseStatus licStatus;
            validationMsg = string.Empty;

            if (Uid == LicenseHandler.GenerateUid())
            {
                validationMsg = "License is valid!";
                if (ExpireDateTime < DateTime.UtcNow || CreateDateTime > DateTime.UtcNow)
                {
                    validationMsg = "License is expired!";
                    licStatus = LicenseStatus.Expired;
                }
                else
                {
                    licStatus = LicenseStatus.Valid;
                }                
            }
            else
            {
                validationMsg = "License is not a valid copy";
                licStatus = LicenseStatus.Invalid;
            }

            return licStatus;
        }
    }
}
