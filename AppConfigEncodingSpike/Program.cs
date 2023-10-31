using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AppConfigEncodingSpike
{
    internal class Program
    {

        // Show how to use a custom protected configuration
        // provider.
        public class TestingProtectedConfigurationProvider
        {

            // Protect the connectionStrings section.
            private static void ProtectConfiguration()
            {

                // Get the application configuration file.
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Define the provider name.
                string provider = "SymmetricProtectedConfigurationProvider";

                // Get the section to protect.
                ConfigurationSection connStrings = config.ConnectionStrings;

                if (connStrings != null)
                {
                    if (!connStrings.SectionInformation.IsProtected)
                    {
                        if (!connStrings.ElementInformation.IsLocked)
                        {
                            // Protect the section.
                            connStrings.SectionInformation.ProtectSection(provider);

                            connStrings.SectionInformation.ForceSave = true;
                            config.Save(ConfigurationSaveMode.Full);

                            Console.WriteLine("Section {0} is now protected by {1}",
                                connStrings.SectionInformation.Name,
                                connStrings.SectionInformation.ProtectionProvider.Name);
                        }
                        else
                            Console.WriteLine(
                                 "Can't protect, section {0} is locked",
                                 connStrings.SectionInformation.Name);
                    }
                    else
                        Console.WriteLine(
                            "Section {0} is already protected by {1}",
                            connStrings.SectionInformation.Name,
                            connStrings.SectionInformation.ProtectionProvider.Name);
                }
                else
                    Console.WriteLine("Can't get the section {0}",
                        connStrings.SectionInformation.Name);
            }

            // Unprotect the connectionStrings section.
            private static void UnProtectConfiguration()
            {
                // Get the application configuration file.
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Get the section to unprotect.
                ConfigurationSection connStrings = config.ConnectionStrings;

                if (connStrings != null)
                {
                    if (connStrings.SectionInformation.IsProtected)
                    {
                        if (!connStrings.ElementInformation.IsLocked)
                        {
                            // Unprotect the section.
                            connStrings.SectionInformation.UnprotectSection();

                            connStrings.SectionInformation.ForceSave = true;
                            config.Save(ConfigurationSaveMode.Full);

                            Console.WriteLine("Section {0} is now unprotected.",
                                connStrings.SectionInformation.Name);
                        }
                        else
                            Console.WriteLine(
                                 "Can't unprotect, section {0} is locked",
                                 connStrings.SectionInformation.Name);
                    }
                    else
                        Console.WriteLine(
                            "Section {0} is already unprotected.",
                            connStrings.SectionInformation.Name);
                }
                else
                    Console.WriteLine("Can't get the section {0}",
                        connStrings.SectionInformation.Name);
            }


            private static void ShowState()
            {
                // Get the application configuration file.
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Get the section to unprotect.
                ConfigurationSection connStrings = config.ConnectionStrings;

                if (connStrings != null)
                {
                    if (connStrings.SectionInformation.IsProtected)
                    {
                        if (!connStrings.ElementInformation.IsLocked)
                        {
                            Console.WriteLine("Section {0} is protected (not locked).",
                                connStrings.SectionInformation.Name);
                        }
                        else
                            Console.WriteLine(
                                 "Section {0} is protected and locked",
                                 connStrings.SectionInformation.Name);
                    }
                    else
                        Console.WriteLine(
                            "Section {0} is unprotected.",
                            connStrings.SectionInformation.Name);


                    if (connStrings is ConnectionStringsSection css)
                    {
                        if (css.ConnectionStrings.Count <= 0)
                        {
                            Console.WriteLine("Section {0} is a connection strings section; Count=0", connStrings.SectionInformation.Name);
                        }
                        else
                        {
                            Console.WriteLine("Section {0} is a connection strings section; Count={1}",
                                connStrings.SectionInformation.Name,
                                css.ConnectionStrings.Count);

                            for (int i = 0; i < css.ConnectionStrings.Count; i++)
                            {
                                Console.WriteLine("  {0} --> \"{1}\"",
                                    i + 1,
                                    css.ConnectionStrings[i]);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Section {0} is not a connection strings section", connStrings.SectionInformation.Name);
                    }
                }
                else
                    Console.WriteLine("Can't get the section {0}",
                        connStrings.SectionInformation.Name);
            }


            public static void Main(string[] args)
            {

                if (args.Length <= 0)
                {
                    ShowHelp();
                    return;
                }

                try
                {

                    string selection = args[0].ToLower();

                    switch (selection)
                    {
                        case "protect":
                            ProtectConfiguration();
                            break;

                        case "unprotect":
                            UnProtectConfiguration();
                            break;

                        case "show":
                            ShowState();
                            break;

                        default:
                            ShowHelp();
                            break;
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Failure - Exception of type [{ex.GetType().Name}]: \"{ex.Message}\"");
                    Console.WriteLine();
                }

                Console.WriteLine();
                Console.Write("Press <ENTER> to exit...");
                Console.ReadLine();
            }
        }


        private static void ShowHelp()
        {
            string appExeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            Console.WriteLine();
            Console.WriteLine($"Using: {appExeName} show|protect|unprotect");
            Console.WriteLine();
            return;
        }
    }
}
