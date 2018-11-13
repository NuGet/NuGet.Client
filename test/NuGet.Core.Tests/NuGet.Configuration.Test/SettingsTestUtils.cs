// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NuGet.Shared;

namespace NuGet.Configuration.Test
{
    public static class SettingsTestUtils
    {
        public static void CreateConfigurationFile(string configurationPath, string mockBaseDirectory, string configurationContent)
        {
            Directory.CreateDirectory(mockBaseDirectory);
            using (var file = File.Create(Path.Combine(mockBaseDirectory, configurationPath)))
            {
                var info = new UTF8Encoding(true).GetBytes(configurationContent);
                file.Write(info, 0, info.Count());
            }
        }

        public static byte[] GetFileHash(string fileName)
        {
            using (var hashAlgorithm = SHA512.Create())
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }

        public static string RemoveWhitespace(string s)
        {
            return Regex.Replace(s, @"\s+", string.Empty);
        }

        public static bool SequenceDeepEquals<T>(IReadOnlyList<T> settings1, IReadOnlyList<T> settings2) where T : SettingBase
        {
            if (settings1 == null && settings2 == null)
            {
                return true;
            }

            if (settings1 == null || settings2 == null)
            {
                return false;
            }

            if (settings1.Count != settings2.Count)
            {
                return false;
            }

            for (var i = 0; i < settings1.Count; i++)
            {
                var val2 = settings2.Where(s => s.Equals(settings1[i]));

                if (val2 == null || val2.Count() != 1)
                {
                    return false;
                }

                if (!DeepEquals(settings1[i], val2.First()))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool DeepEquals(SettingBase setting1, SettingBase setting2)
        {
            if (setting1 is SettingSection)
            {
                return Section_DeepEquals(setting1 as SettingSection, setting2 as SettingSection);
            }
            else if (setting1 is AddItem)
            {
                return AddItem_DeepEquals(setting1 as AddItem, setting2 as AddItem);
            }
            else if (setting1 is CredentialsItem)
            {
                return CredentialsItem_DeepEquals(setting1 as CredentialsItem, setting2 as CredentialsItem);
            }
            else if (setting1 is UnknownItem)
            {
                return UnkownItem_DeepEquals(setting1 as UnknownItem, setting2 as UnknownItem);
            }
            else if (setting1 is ClearItem clear1)
            {
                return clear1.Equals(setting2 as ClearItem);
            }
            else if (setting1 is SettingText text1)
            {
                return text1.Equals(setting2 as SettingText);
            }
            else if (setting1 is RepositoryItem)
            {
                return RepositoryItem_DeepEquals(setting1 as RepositoryItem, setting2 as RepositoryItem);
            }
            else if (setting1 is AuthorItem)
            {
                return AuthorItem_DeepEquals(setting1 as AuthorItem, setting2 as AuthorItem);
            }
            else if (setting1 is OwnersItem)
            {
                return OwnersItem_DeepEquals(setting1 as OwnersItem, setting2 as OwnersItem);
            }
            else if (setting1 is CertificateItem)
            {
                return CertificateItem_DeepEquals(setting1 as CertificateItem, setting2 as CertificateItem);
            }

            return false;
        }

        private static bool ItemBase_DeepEquals(SettingItem item1, SettingItem item2)
        {
            if (item1 == null || item2 == null)
            {
                return item1 == null && item2 == null;
            }

            if (!item1.Equals(item2))
            {
                return false;
            }

            if (item1.Attributes.Count == item2.Attributes.Count)
            {
                return item1.Attributes.OrderedEquals(item2.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool AddItem_DeepEquals(AddItem item1, AddItem item2)
        {
            return ItemBase_DeepEquals(item1, item2);
        }

        private static bool CredentialsItem_DeepEquals(CredentialsItem item1, CredentialsItem item2)
        {
            if (!ItemBase_DeepEquals(item1, item2))
            {
                return false;
            }

            var validAutheticationTypesEquals = string.IsNullOrEmpty(item1.ValidAuthenticationTypes) ?
               string.IsNullOrEmpty(item2.ValidAuthenticationTypes) :
               string.Equals(item1.ValidAuthenticationTypes, item2.ValidAuthenticationTypes, StringComparison.Ordinal);

            return string.Equals(item1.Username, item2.Username, StringComparison.Ordinal)
                && item1.IsPasswordClearText == item2.IsPasswordClearText
                && string.Equals(item1.Password, item2.Password, StringComparison.Ordinal)
                && validAutheticationTypesEquals;
        }

        private static bool Section_DeepEquals(SettingSection section1, SettingSection section2)
        {
            if (section1.Attributes.Count == section2.Attributes.Count &&
                section1.Items.Count == section2.Items.Count)
            {
                var attributesEquals = section1.Attributes.OrderedEquals(section2.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase);
                var itemsEquals = true;

                var items1 = section1.Items.ToList();
                var items2 = section2.Items.ToList();

                for (var i = 0; i < items1.Count; i++)
                {
                    itemsEquals &= DeepEquals(items1[i], items2[i]);
                }

                return itemsEquals && attributesEquals;
            }

            return false;
        }

        private static bool UnkownItem_DeepEquals(UnknownItem item1, UnknownItem item2)
        {
            if (!ItemBase_DeepEquals(item1, item2))
            {
                return false;
            }

            if (item1.Children.Count == item2.Children.Count)
            {
                var childEquals = true;

                var children1 = item1.Children.ToList();
                var children2 = item2.Children.ToList();

                for (var i = 0; i < children1.Count; i++)
                {
                    childEquals &= DeepEquals(children1[i], children2[i]);
                }

                return childEquals;
            }

            return false;
        }

        private static bool RepositoryItem_DeepEquals(RepositoryItem item1, RepositoryItem item2)
        {
            if (!ItemBase_DeepEquals(item1, item2))
            {
                return false;
            }

            if (item1.Certificates.Count == item2.Certificates.Count)
            {
                var ownersEquals = item1.Owners.SequenceEqual(item2.Owners);
                var itemsEquals = true;

                var certificate1 = item1.Certificates;
                var certificate2 = item2.Certificates;

                for (var i = 0; i < certificate1.Count; i++)
                {
                    itemsEquals &= DeepEquals(certificate1[i], certificate2[i]);
                }

                return itemsEquals && ownersEquals;
            }

            return false;
        }

        private static bool AuthorItem_DeepEquals(AuthorItem item1, AuthorItem item2)
        {
            if (!ItemBase_DeepEquals(item1, item2))
            {
                return false;
            }

            if (item1.Certificates.Count == item2.Certificates.Count)
            {
                var itemsEquals = true;

                var certificate1 = item1.Certificates;
                var certificate2 = item2.Certificates;

                for (var i = 0; i < certificate1.Count; i++)
                {
                    itemsEquals &= DeepEquals(certificate1[i], certificate2[i]);
                }

                return itemsEquals;
            }

            return false;
        }

        private static bool OwnersItem_DeepEquals(OwnersItem item1, OwnersItem item2)
        {
            if (!ItemBase_DeepEquals(item1, item2))
            {
                return false;
            }

            return item1.Content.SequenceEqual(item2.Content, StringComparer.Ordinal);
        }

        private static bool CertificateItem_DeepEquals(CertificateItem item1, CertificateItem item2)
        {
            return ItemBase_DeepEquals(item1, item2);
        }
    }
}
