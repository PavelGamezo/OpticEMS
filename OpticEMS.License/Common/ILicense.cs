namespace OpticEMS.License.Common
{
    public interface ILicense
    {
        string AppName { get; }

        string Uid { get; set; }

        DateTime CreateDateTime { get; set; }

        DateTime ExpireDateTime { get; set; }

        LicenseStatus DoExtraValidation(out string validationMsg);
    }
}
