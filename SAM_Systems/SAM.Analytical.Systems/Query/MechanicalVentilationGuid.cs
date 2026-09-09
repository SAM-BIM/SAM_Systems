// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>
        /// The identity schema marker, hashed first. <b>Bump it only for a deliberate, breaking change to
        /// what the key is made of</b> - doing so re-keys every materialised object in existence, which is
        /// the point: a key derived under different rules must not be mistaken for one derived under
        /// these.
        /// <para>
        /// <c>static readonly</c> rather than <c>const</c>, which the compiler would inline into every
        /// consuming assembly - so a bump here would leave any assembly that was not rebuilt deriving keys
        /// under the old marker while believing it was current.
        /// </para>
        /// </summary>
        public static readonly string MechanicalVentilationIdentitySchema = "MechanicalVentilationMaterialisation:v1";

        /// <summary>
        /// Namespace for the derivation, so a materialised identity can only ever collide with another
        /// materialised identity and never with a real model guid or an assessment key.
        /// </summary>
        private static readonly Guid guid_Namespace_MechanicalVentilation = new Guid("6f2a1c84-93d7-4e51-8b0a-2c6e14d9f7a3");

        /// <summary>
        /// A deterministic guid for one materialised object, derived from what the object <i>is</i> - its
        /// analytical source, its air system, its template prototype - and never from a name, an
        /// enumeration index or a position in a file.
        /// <para>
        /// <b>The convention is <c>OverheatingScenario</c>'s, reused verbatim rather than reinvented:</b>
        /// the namespace bytes, then the schema, then every component UTF-8 and length-prefixed (null
        /// written as length -1, so it is distinct from empty, and normalised to NFC), SHA-256, the first
        /// sixteen bytes stamped with version 8 and the RFC 4122 variant. The hash is a spreading function
        /// and not a security primitive - the whole point is that it is reproducible, which is why it is
        /// not salted and must never become so.
        /// </para>
        /// <para>
        /// <b>Domain separation.</b> <paramref name="domain"/> is written straight after the schema, so no
        /// combination of one domain's components can produce another domain's key - a supply and an
        /// extract connection on the same space in the same system differ only by the domain tag, and must.
        /// </para>
        /// </summary>
        /// <param name="domain">The kind of object being keyed - "AirSystem", "SystemSpace", "SupplyConnection", …</param>
        /// <param name="components">
        /// The identity components, in a fixed order. Sets are sorted by the caller before they get here,
        /// so nothing about the key depends on the order the model was enumerated in.
        /// </param>
        internal static Guid MechanicalVentilationGuid(string domain, params string[] components)
        {
            List<byte> bytes = new List<byte>(guid_Namespace_MechanicalVentilation.ToByteArray());

            //First, and before anything that could change: a key derived under a different schema must be
            //a different key.
            Append(bytes, MechanicalVentilationIdentitySchema);

            Append(bytes, domain);

            //Counted first, so a trailing component cannot be confused with a longer value on the one
            //before it.
            AppendLength(bytes, components == null ? 0 : components.Length);

            if (components != null)
            {
                foreach (string component in components)
                {
                    Append(bytes, component);
                }
            }

            byte[] hash;

            using (System.Security.Cryptography.SHA256 sHA256 = System.Security.Cryptography.SHA256.Create())
            {
                hash = sHA256.ComputeHash(bytes.ToArray());
            }

            byte[] result = new byte[16];
            Array.Copy(hash, result, 16);

            //Version 8 (custom) and the RFC 4122 variant, so this is a valid guid and is visibly not a
            //model identity that happens to look similar.
            result[7] = (byte)((result[7] & 0x0F) | 0x80);
            result[8] = (byte)((result[8] & 0x3F) | 0x80);

            return new Guid(result);
        }

        /// <summary>A guid as a key component, formatted invariantly so it goes through one encoding rule.</summary>
        internal static string MechanicalVentilationGuidComponent(Guid guid)
        {
            return guid.ToString("D", CultureInfo.InvariantCulture);
        }

        private static void Append(List<byte> bytes, string text)
        {
            if (text == null)
            {
                AppendLength(bytes, -1);
                return;
            }

            string text_Canonical;

            try
            {
                text_Canonical = text.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                text_Canonical = text;
            }

            byte[] bytes_Text = Encoding.UTF8.GetBytes(text_Canonical);

            AppendLength(bytes, bytes_Text.Length);
            bytes.AddRange(bytes_Text);
        }

        /// <summary>
        /// Writes an int as four bytes, least significant first, explicitly rather than through
        /// <c>BitConverter</c> - which is endian-dependent, and a key must not depend on the architecture
        /// that derived it.
        /// </summary>
        private static void AppendLength(List<byte> bytes, int value)
        {
            unchecked
            {
                bytes.Add((byte)value);
                bytes.Add((byte)(value >> 8));
                bytes.Add((byte)(value >> 16));
                bytes.Add((byte)(value >> 24));
            }
        }
    }
}
