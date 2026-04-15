using OpticEMS.License.Common;
using OpticEMS.License.Helpers;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OpticEMS.License.Handlers
{
    public static class LicenseHandler
    {
        public static string GenerateUid()
        {
            return LicenseHelper.GenerateUid();
        }

        public static string GenerateLicense(Common.License license,
            byte[] certificatePrivateKeyData,
            SecureString certificatePassword)
        {
            var licenseObject = new XmlDocument();
            using (var writer = new StringWriter())
            {
                var serializer = new XmlSerializer(typeof(Common.License), new[] { license.GetType() });

                serializer.Serialize(writer, license);

                licenseObject.LoadXml(writer.ToString());   
            }

            var cert = new X509Certificate2(certificatePrivateKeyData, certificatePassword);
            var a = cert.GetRSAPrivateKey();

            SignXml(licenseObject, a);

            var licenseKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(licenseObject.OuterXml));
            
            return licenseKey;
        }

        private static void SignXml(XmlDocument xmlDoc, AsymmetricAlgorithm key)
        {
            if (xmlDoc == null)
            {
                throw new ArgumentException("xmlDoc");
            }
            if (key == null)
            {
                throw new ArgumentException("key");
            }

            var signedXml = new SignedXml(xmlDoc) { SigningKey = key };

            var reference = new Reference { Uri = "" };

            var env = new XmlDsigEnvelopedSignatureTransform();

            reference.AddTransform(env);

            signedXml.AddReference(reference);
            signedXml.ComputeSignature();

            var xmlDigitalSignature = signedXml.GetXml();

            xmlDoc.DocumentElement?.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
        }

        public static Common.License ParseLicenseFromBase64String(string licenseString,
            byte[] certPubKeyData, out LicenseStatus licStatus, out string validationMsg)
        {
            validationMsg = string.Empty;
            licStatus = LicenseStatus.Undefined;

            if (string.IsNullOrWhiteSpace(licenseString))
            {
                licStatus = LicenseStatus.Cracked;
                validationMsg = "Your copy of this application is cracked";
                return null;
            }

            Common.License lic = null;

            try
            {
                var cert = new X509Certificate2(certPubKeyData);
                var rsaKey = cert.GetRSAPublicKey();

                var xmlDoc = new XmlDocument { PreserveWhitespace = true };
                xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(licenseString)));

                if (VerifyXml(xmlDoc, rsaKey))
                {
                    var nodeList = xmlDoc.GetElementsByTagName("Signature");
                    xmlDoc.DocumentElement?.RemoveChild(nodeList[0]);

                    var licXml = xmlDoc.OuterXml;

                    var serializer = new XmlSerializer(typeof(Common.License));
                    using (var reader = new StringReader(licXml))
                    {
                        var obj = serializer.Deserialize(reader);
                        lic = (Common.License)obj;
                    }

                    licStatus = lic.DoExtraValidation(out validationMsg);
                }
                else
                {
                    licStatus = LicenseStatus.Invalid;
                    validationMsg = "Your copy of this application is not activated";
                }
            }
            catch(Exception ex)
            {
                licStatus = LicenseStatus.Cracked;
                validationMsg = "Your copy of this application is cracked";
            }

            return lic;
        }

        private static bool VerifyXml(XmlDocument doc, AsymmetricAlgorithm key)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var signedXml = new SignedXml(doc);

            var nodeList = doc.GetElementsByTagName("Signature");

            if (nodeList.Count <= 0)
            {
                throw new CryptographicException(
                    "Verification failed: No Signature was found in the document.");
            }

            if (nodeList.Count >= 2) 
            {
                throw new CryptographicException(
                    "Verification failed: More that one signature was found for the document.");
            }

            signedXml.LoadXml((XmlElement)nodeList[0]);

            return signedXml.CheckSignature(key);
        }
    }
}
