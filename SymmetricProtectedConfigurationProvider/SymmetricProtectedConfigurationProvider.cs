using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace de.Aargenveldt.Auxiliary.Configuration.Encoding.ConfigurationProviders
{
    /// <summary>
    /// Provider für die Ver-/Entschlüsselung von Sektionen in <c>app.config</c> oder <c>web.config</c>
    /// Dateien. Verwendet symmetrische Ver-/Entschlüsselung auf Basis von AES. - NICHT FÜR DEN PRODUKTIVEN
    /// BETRIEB GEEIGNET.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Diese Implementierung dient lediglich als Prototyp und Demonstration.
    ///         Sie ist NICHT (gut) für den produktiven Einsatz geeignet, da sich das
    ///         Geheimnis für Ver-/Entschlüsselung prinzipbedingt aus dem Code der
    ///         nutzenden Anwendung (oder der Konfiguration) ablesen lässt...
    ///     </para>
    /// </remarks>
    public class SymmetricProtectedConfigurationProvider : ProtectedConfigurationProvider, IDisposable
    {

        private string _name;
        private string _seed;
        private string _salt;

        private Aes _aes;

        private bool _disposed = false;


        /// <inheritdoc />
        public override string Name
        {
            get { return this._name; }
        }

        /// <summary>
        /// Liefert den Stammwert für die Schlüsselableitung.
        /// </summary>
        public string Seed => this._seed;

        /// <summary>
        /// Liefert das Salt für die Schlüsselableitung.
        /// </summary>
        public string Salt => this._salt;


        /// <inheritdoc/>
        /// <remarks>
        ///     <para>
        ///         Folgende Konfigurationswerte werden verarbeitet:
        ///         <list type="table">
        ///             <item>
        ///                 <term>seed</term>
        ///                 <description>Stammwert für die Schlüsselableitung</description>
        ///             </item>
        ///             <item>
        ///                 <term>salt</term>
        ///                 <description>Salt für die Schlüsselableitung</description>
        ///             </item>
        ///         </list>
        ///     </para>
        /// </remarks>
        /// <exception cref="ConfigurationErrorsException">
        ///     <list type="bullet">
        ///         <item><description>Ein Eintrag für &quot;seed&quot; fehlt in <paramref name="config"/> oder
        ///         der vorhandene Eintrag ist <see langword="null"/>, leer oder besteht nur aus Leerzeichen</description></item>
        ///         <item><description>Ein Eintrag für &quot;salt&quot; fehlt in <paramref name="config"/> oder
        ///         der vorhandene Eintrag ist <see langword="null"/>, leer oder besteht nur aus Leerzeichen</description></item>
        ///     </list>
        /// </exception>
        public override void Initialize(string name, NameValueCollection config)
        {
            this._name = name;
            string seed = this._seed = config["seed"];
            string salt = this._salt = config["salt"];


            if (string.IsNullOrWhiteSpace(seed))
                throw new ConfigurationErrorsException("No key derivation seed provided");
            if (string.IsNullOrWhiteSpace(salt))
                throw new ConfigurationErrorsException("No key derivation salt provided");

            this.CreateKey(seed, salt);
            return;
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     <para>
        ///         Führt eine symmetrische Verschlüsselung des übergebenen XML Knotens 
        ///         (<c>OuterXml</c>) aus. Der Schlüssel leitet sich aus dem bei der
        ///         Initialisierung angegebenen Stammwert ab.
        ///     </para>
        /// </remarks>
        public override XmlNode Encrypt(XmlNode node)
        {
            string encryptedData = this.EncryptString(node?.OuterXml);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml($"<EncryptedData>{encryptedData}</EncryptedData>");

            return xmlDoc.DocumentElement;
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     <para>
        ///         Führt eine symmetrische Entschlüsselung des Inhalts (<c>InnerXml</c>) des
        ///         übergebenen XML Knotens aus. Der Schlüssel leitet sich aus dem bei der
        ///         Initialisierung angegebenen Stammwert ab.
        ///     </para>
        /// </remarks>
        public override XmlNode Decrypt(XmlNode encryptedNode)
        {
            string decryptedData = this.DecryptString(encryptedNode?.InnerText);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(decryptedData);

            return xmlDoc.DocumentElement;
        }

        /// <summary>
        /// Verschlüsselt eine Zeichenfolge.
        /// </summary>
        /// <param name="plainText">Klartext, der verschlüsselt werden soll; darf <see langword="null"/> oder leer sein</param>
        /// <returns>
        ///     Verschlüsselte Zeichenfolge im Base64 Format; eine leere zeichenfolge, falls
        ///     <paramref name="plainText"/> <see langword="null"/> oder leer war
        /// </returns>
        private string EncryptString(string plainText)
        {
            string retval;

            if (true == string.IsNullOrEmpty(plainText))
            {
                retval = string.Empty;
            }
            else
            {
                using (ICryptoTransform transform = this._aes.CreateEncryptor())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, transform, CryptoStreamMode.Write))
                        using (StreamWriter sw = new StreamWriter(cs, System.Text.Encoding.UTF8))
                        {
                            sw.Write(plainText);
                        }
                        retval = Convert.ToBase64String(ms.ToArray());
                    }
                }
            }

            return retval;
        }

        /// <summary>
        /// Entschlüsselt eine Zeichenfolge, die verschlüsselten Inhalt als Base64 kodierte Bytefolge enthält.
        /// </summary>
        /// <param name="encryptedValue">Verschlüsselte Zeichenfolge in Bae64 Kodierung, die entschlüsselt werden soll</param>
        /// <returns>Entschlüsselte Zeichenfolge; eine leere Zeichenfolge, falls <paramref name="encryptedValue"/>
        /// <see langword="null"/> oder leer ist</returns>
        /// <exception cref="FormatException">
        ///     <paramref name="encryptedValue"/> ist weder <see langword="null"/>, leer noch eine gültige Base64
        ///     kodierte Zeichenfolge.
        /// </exception>
        private string DecryptString(string encryptedValue)
        {
            string retval;

            if (true == string.IsNullOrEmpty(encryptedValue))
            {
                retval = string.Empty;
            }
            else
            {
                using (ICryptoTransform transform = this._aes.CreateDecryptor())
                {
                    byte[] encryptedBlock = Convert.FromBase64String(encryptedValue);
                    using (MemoryStream ms = new MemoryStream(encryptedBlock, false))
                    using (CryptoStream cs = new CryptoStream(ms, transform, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs, System.Text.Encoding.UTF8))
                        {
                            retval = sr.ReadToEnd();
                        }
                    }
                }
            }

            return retval;
        }

        /// <summary>
        /// Erzeugt ein Verschlüsselungsobjekt und initialisiert es an Hand von <paramref name="seed"/>
        /// und <paramref name="salt"/>. Ein bereits bestehendes Verschlüsselungsobjekt wird verworfen.
        /// </summary>
        /// <param name="seed">Stammwert für die Schlüsselableitung</param>
        /// <param name="salt">Salt für die Schlüsselableitung</param>
        public void CreateKey(string seed, string salt)
        {
            if (true == string.IsNullOrEmpty(seed))
                throw new ArgumentException("No key derivation seed supplied", nameof(seed));
            if (true == string.IsNullOrEmpty(salt))
                throw new ArgumentException("No key derivation salt supplied", nameof(salt));

            this.DropAesInstance();

            byte[] binarySalt = this.PrepareSalt(salt);


            AesManaged aes = new AesManaged();
            using (Rfc2898DeriveBytes bytesGenerator = new Rfc2898DeriveBytes(seed, binarySalt))
            {
                int keySizeInBits = aes.LegalKeySizes.First().MaxSize;
                aes.Key = bytesGenerator.GetBytes(keySizeInBits / 8);
                aes.IV = bytesGenerator.GetBytes(aes.BlockSize / 8);

                this._aes = aes;
            }
            return;
        }



        /// <summary>
        /// Gibt ein intern gesetztes AES Verschlüsselungsobjekt frei.
        /// </summary>
        private void DropAesInstance()
        {
            IDisposable disposableObject = Interlocked.Exchange(ref this._aes, null) as IDisposable;
            try
            {
                disposableObject?.Dispose();
            }
            catch { }
            return;
        }


        /// <summary>
        /// Bereitet eine Salt Angabe auf und erstellt daraus ein Byte Array.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Ein Salt kann als Hexstring, Base64 String oder beliebige Zeichenkette angegeben
        ///         werden. Lässt es sich als Hexstring oder Base64 String interpertieren, wird es
        ///         gemäß dieser Kodierungen in ein Byte Array umgesetzt - ansonsten wird die Zeichenkette
        ///         als UTF8 String in eine Bytefolge umgesetzt.
        ///     </para>
        /// </remarks>
        /// <param name="salt">Salt als Hexstring (ggf. mit Präfix), Base64 String - oder beliebige Zeichenkette</param>
        /// <returns>
        ///     Byte Array, dass sich aus der Salt Angabe ergeben hat (Umsetzung aus Hexstring, Base64 String
        ///     oder UTF8 Bytefolge der Zeichenkette); ein leeres Byte Array, falls <paramref name="salt"/>
        ///     <see langword="null"/> oder leer ist.
        /// </returns>
        private byte[] PrepareSalt(string salt)
        {
            byte[] retval;
            if (true == string.IsNullOrEmpty(salt))
                retval = Array.Empty<byte>();
            else
            {
                try
                {
                    retval = this.HexToByte(salt);
                }
                catch
                {
                    try
                    {
                        retval = Convert.FromBase64String(salt);
                    }
                    catch
                    {
                        retval = System.Text.Encoding.UTF8.GetBytes(salt);
                    }
                }
            }

            return retval;
        }


        /// <summary>
        /// Konvertiert ein Byte Array in einen Hexstring.
        /// </summary>
        /// <param name="byteArray">Byte Array, das umgesetzt werden soll; darf leer oder <see langword="null"/> sein</param>
        /// <returns>Hexstring (ohne Präfix); ein Leerstring, falls <paramref name="byteArray"/> leer oder <see langword="null"/> ist</returns>
        private string ByteToHex(byte[] byteArray)
        {
            if ((byteArray == null) || (byteArray.Length == 0))
                return string.Empty;
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (Byte b in byteArray)
                    sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Konvertiert einen hexadezimalen String in ein Byte Array. Hat der String einen Präfix
        /// (&quot;$&quot;, &quot;0x&quot;, &quot;&amp;h&quot;), dann wird der ignoriert. Es wird
        /// erwartet, dass die Stellenzahl (ggf. ohne den Präfix) gerade ist. Die Groß-/Kleinschreibung
        /// spielt keine Rolle.
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns>
        ///     Byte Array Entsprechung des Hexstrings; ein leeres Array, wenn <paramref name="hexString"/>
        ///     <see langword="null"/> oder leer ist - oder nur aus Leerzeichen oder einem Präfix besteht
        /// </returns>
        /// <exception cref="FormatException">
        ///     Die Zeichenkette kann nicht als Hexstring interpretiert werden (ggf. Details in 
        ///     <see cref="System.Exception.InnerException"/>).
        /// </exception>
        private byte[] HexToByte(string hexString)
        {
            byte[] retval;
            if (true == string.IsNullOrWhiteSpace(hexString))
                retval = Array.Empty<byte>();
            else
            {
                try
                {
                    int startOffset = 0;
                    if (true == hexString.StartsWith("$"))
                        startOffset = 1;
                    else if (true == hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        startOffset = 2;
                    else if (true == hexString.StartsWith("&h", StringComparison.OrdinalIgnoreCase))
                        startOffset = 2;

                    retval = new byte[(hexString.Length - startOffset) / 2];
                    for (int i = 0; i < retval.Length; i++)
                        retval[i] = Convert.ToByte(hexString.Substring(i * 2 + startOffset, 2), 16);
                }
                catch (FormatException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Converting hex string to byte array failed: \"{hexString}\"", ex);
                }
            }
            return retval;
        }



        /// <summary>
        /// Ressourcen freigeben.
        /// </summary>
        /// <param name="disposing">Wenn <see langword="true"/>, dann expliziter Aufruf; ansonsten Aufruf
        /// durch den GC</param>
        protected virtual void Dispose(bool disposing)
        {
            if (false == this._disposed)
            {
                if (true == disposing)
                {
                    // Verwalteten Zustand (verwaltete Objekte) bereinigen
                    this.DropAesInstance();
                }

                // Nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben und Finalizer überschreiben
                // NOP

                // Felder auf NULL setzen
                this._aes = null;

                this._disposed = true;
            }

            return;
        }

        // // TODO: Finalizer nur überschreiben, wenn "Dispose(bool disposing)" Code für die Freigabe nicht verwalteter Ressourcen enthält
        // ~SymmetricProtectedConfigurationProvider()
        // {
        //     // Diesen Code nicht ändern. Bereinigungscode in der Methode "Dispose(bool disposing)" einfügen.
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Diesen Code nicht ändern. Bereinigungscode in der Methode "Dispose(bool disposing)" einfügen.
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
            return;
        }
    }
}
