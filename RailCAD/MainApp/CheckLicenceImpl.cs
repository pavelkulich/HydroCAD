using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using System.Windows;

using DeviceId;

using RailCAD.Common;
using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Views;

namespace RailCAD.MainApp
{
    /// <summary>
    /// Licence type. Excatly 4 character string. Signed licence format is base64 encoded.
    /// </summary>
    public enum LicenceType
    {
        INVALID = 0,  // default
        FULL = 1,
        STUD= 2,  // student licence (for educational use only)
    }

    partial class RCApp
    {
        private const string PUBLIC_KEY_XML = "<RSAKeyValue>" +
            "<Modulus>73yrByvHSAvnTnW3dTIIPLwArrGOZXpxrWm1RjdEAhihb48OlfMBHr5cw7N+ZWUZNyx1mwfeUAREFrIHmAz84mQuOYWo61ovvtZ4r7I3RzEkzCBMhmPLK8a0pE0qVgio0ZDtaywE1kHnWtpCOWeqiAFFAXWE/O6vB5dyHdqbv8wfQZRARVCSZ6e5KYFmiGpBKL5/hwpu3kz0D/u9yrncrupgVhfOLnJliJRyaNQ0+LLUJastMgrWNVIFuyh8xGQaRiTye9h6mBxWeAPCkHqrEKCA2jKAVXcZGIFF9rjkfl6BDivOCB+eJgyCEZNfURtReqXCGzz5c5YPEum7KHo4HQ==</Modulus>" +
            "<Exponent>AQAB</Exponent>" +
            "</RSAKeyValue>";

        private static LicenceType CheckLicence(ICadModel cad, bool fillLispResp = false)
        {
            string licenceOption = (string)cad.ReadAndValidateLispArgs(ResBufIO.ReadRCLicenceInput);
            bool forceRegnerate = licenceOption != null && licenceOption.Equals("REGENERATE", StringComparison.OrdinalIgnoreCase);

            LicenceType licence = LicenceType.INVALID;
            string computerId = new DeviceIdBuilder()
                .OnWindows(windows => windows.AddWindowsDeviceId())
                //.UseFormatter(DeviceIdFormatters.DefaultV6)  // default formatter is V6 (base32 Crockford) (ensure length is multiples of 4)
                .ToString();

            //XmlDocument doc = new XmlDocument();
            //string publicKeyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "LicenceKeyPublic.xml");
            //doc.Load(publicKeyPath);
            //string publicKeyXml = doc.InnerXml;
            string publicKeyXml = PUBLIC_KEY_XML;  // public key as a hardcoded string

            string licenceFilePath = Path.Combine(RCPaths.GetAppDataPath(), "LicenceFile.lic");

            if (File.Exists(licenceFilePath) && !forceRegnerate)
            {
                string licenceText = File.ReadAllText(licenceFilePath);

                licence = VerifyLicence(computerId, licenceText, publicKeyXml);

                if (licence == LicenceType.INVALID)  // invalid licence
                {
                    MessageBox.Show(Properties.Resources.LicenceInvalidWarning, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                cad.WriteMessage($"Existing licence: {licence}");
            }
            else
            {
                string encryptedComputerId = Encrypt(computerId, publicKeyXml);

                var dialog = new LicenceActivationDialog(encryptedComputerId);

                if (dialog.ShowDialog() == true)
                {
                    // User clicked OK - process the licence key
                    string signedLicence = dialog.LicenceKey;

                    licence = VerifyLicence(computerId, signedLicence, publicKeyXml);

                    if (licence != LicenceType.INVALID)
                    {
                        File.WriteAllText(licenceFilePath, signedLicence);
                        MessageBox.Show(Properties.Resources.LicenceActivationSuccess, Properties.Resources.Success, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(Properties.Resources.LicenceActivationFailed, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // User clicked Cancel or closed the dialog
                    MessageBox.Show(Properties.Resources.LicenceActivationAborted, Properties.Resources.Aborted, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                cad.WriteMessage($"New licence: {licence}");  // debug
            }

            if (fillLispResp)
            {
                cad.SetLispResp(ResBufIO.WriteLicenceResp, licence);  // set response for lisp function use
            }
            return licence;  // return result for direct use
        }

        private static string Encrypt(string text, string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKey);
                var bytesPlainTextData = System.Text.Encoding.Unicode.GetBytes(text);

                // RSAEncryptionPadding.Pkcs1: PKCS#1 v1.5 padding (random padding -> output is different every time)
                var bytesCypherText = rsa.Encrypt(bytesPlainTextData, false);

                return Convert.ToBase64String(bytesCypherText);
            }
        }

        private static LicenceType VerifyLicence(string computerId, string signature, string publicKey)
        {
            foreach (LicenceType licenceType in Enum.GetValues(typeof(LicenceType)))
            {
                if (licenceType == LicenceType.INVALID)
                    continue;

                string licenceText = $"{computerId}{licenceType}{RCApp.APP_VERSION}";
                if (Verify(licenceText, signature, publicKey))
                {
                    return licenceType;
                }
            }
            return LicenceType.INVALID;
        }

        private static bool Verify(string licenceText, string signature, string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKey);

                byte[] bytesPlainTextData;
                byte[] bytesSignature;
                try
                {
                    bytesPlainTextData = Convert.FromBase64String(licenceText);
                    bytesSignature = Convert.FromBase64String(signature);
                }
                catch
                {
                    return false;
                }

                bool success = rsa.VerifyData(bytesPlainTextData, bytesSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return success;
            }
        }
    }
}
