// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

namespace Microsoft.OneGet.Packaging {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using Utility.Extensions;

    /// <summary>
    ///     This class represents a package (retrieved from Find-SoftwareIdentity or Get-SoftwareIdentity)
    ///     Will eventually also represent a swidtag.
    ///     todo: Should this be serializable instead?
    /// </summary>
    public class SoftwareIdentity : MarshalByRefObject {
        public override object InitializeLifetimeService() {
            return null;
        }

        #region OneGet specific data
        internal string FastPackageReference {get; set;}

        public string ProviderName {get; internal set;}
        public string Source {get; internal set;}
        public string Status {get; internal set;}


        public string SearchKey {get; internal set;}
        
        public string FullPath {get; internal set;}
        public string PackageFilename {get; internal set;}

        public bool FromTrustedSource {get; internal set;}

        // OneGet shortcut property -- Summary *should* be stored in SoftwareMetadata
        public string Summary {
            get {
                return Swid.Root().Elements(Iso19770_2.Meta).Select( each => each.Get(Iso19770_2.SummaryAttribute)).WhereNotNull().FirstOrDefault();
            }
            internal set {
                Set(Iso19770_2.SummaryAttribute.LocalName, value);
            }
        }
        #endregion

        #region ISO-19770-2-2014 metadata

        public string Name {
            get {
                return Swid.Root().Get(Iso19770_2.NameAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.NameAttribute, value);
            }
        }

        public string Version {
            get {
                return Swid.Root().Get(Iso19770_2.VersionAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.VersionAttribute, value);
            }
        }

        public string VersionScheme {
            get {
                return Swid.Root().Get(Iso19770_2.VersionSchemeAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.VersionSchemeAttribute, value);
            }
        }
        public string TagVersion {
            get {
                return Swid.Root().Get(Iso19770_2.TagVersionAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.TagVersionAttribute, value);
            }
        }

        public string TagId {
            get {
                return Swid.Root().Get(Iso19770_2.TagIdAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.TagIdAttribute, value);
            }
        }

        public bool? IsPatch {
            get {
                return Swid.Root().Get(Iso19770_2.PatchAttribute).IsTrue();
            }
            internal set {
                Swid.Root().Set(Iso19770_2.PatchAttribute, value.ToString());
            }
        }

        public bool? IsSupplemental {
            get {
                return Swid.Root().Get(Iso19770_2.SupplementalAttribute).IsTrue();
            }
            internal set {
                Swid.Root().Set(Iso19770_2.SupplementalAttribute, value.ToString());
            }
        }

        public string AppliesToMedia {  get {
                return Swid.Root().Get(Iso19770_2.MediaAttribute);
            }
            internal set {
                Swid.Root().Set(Iso19770_2.MediaAttribute, value);
            }
        }

        // shortcut for Meta values 
        public IEnumerable<string> this[string index] {
            get {
                return Meta.Where(each => each.ContainsKey(index)).Select(each => each[index]).ByRef();
            }
        }

        internal void Set(string metaKey, string value) {
            var v = this[metaKey].ToArray();

            if (v.Length > 0 && !v.Contains(value)) {
                // if the value is already set, we don't want to re set it.
                // Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "REPLACING {0} in Meta element in swidtag {1} -> {2} ", metaKey, v[0], value));
                throw new Exception("INVALID_SWIDTAG_ATTRIBUTE_VALUE_CHANGE");
            }

            // looks good, let's set it in the first meta element.
            FirstMeta.Set(metaKey, value);
        }

        public IEnumerable<SoftwareMetadata> Meta {
            get {
                return Swid.Root().Elements(Iso19770_2.Meta).Select(each => new SoftwareMetadata(each)).ByRef();
            }
        }

        public IEnumerable<Entity> Entities {
            get {
                return Swid.Root().Elements(Iso19770_2.Entity).Select(each => new Entity(each)).ByRef();
            }
        }

        internal Entity AddEntity(string name, string regid, string role, string thumbprint = null) {
            XElement e;
            Swid.Root().Add( e= new XElement(Iso19770_2.Entity)
                .Set(Iso19770_2.NameAttribute, name )
                .Set(Iso19770_2.RegIdAttribute, regid)
                .Set(Iso19770_2.RoleAttribute, role)
                .Set(Iso19770_2.ThumbprintAttribute,thumbprint)
               );
            return new Entity(e);
        } 

        public IEnumerable<Link> Links {
            get {
                return Swid.Root().Elements(Iso19770_2.Link).Select(each => new Link(each)).ByRef();
            }
        }

        internal Link AddLink(string referenceUri, string relationship, string mediaType, string ownership, string use, string appliesToMedia, string artifact) {
            XElement e;
            Swid.Root().Add(e = new XElement(Iso19770_2.Link)
                .Set(Iso19770_2.HRefAttribute, referenceUri)
                .Set(Iso19770_2.RelationshipAttribute, relationship)
                .Set(Iso19770_2.MediaTypeAttribute, mediaType)
                .Set(Iso19770_2.OwnershipAttribute, ownership)
                .Set(Iso19770_2.UseAttribute, use)
                .Set(Iso19770_2.MediaAttribute, appliesToMedia)
                .Set(Iso19770_2.ArtifactAttribute, artifact)
               );
            return new Link(e);
        } 

#if M2
        public Evidence Evidence {get; internal set;}

        public Payload Payload {get; internal set;}
#endif

        private XElement FirstMeta {
            get {
                var meta = Swid.Root().Elements(Iso19770_2.Meta).FirstOrDefault();
                if (meta == null) {
                    // there isn't one 
                    Swid.Root().Add(meta = new XElement(Iso19770_2.Meta));
                }
                return meta;
            }
        }

        private XDocument _swidTag;
        public XDocument Swid {
            get {
                if (_swidTag == null) {
                    _swidTag = Iso19770_2.NewDocument;
                }
                return _swidTag;
            }
            internal set {
                
            }
        }

        public string SwidTagText {
            get {
                var stringBuilder = new StringBuilder();

                var settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = false;
                settings.Indent = true;
                settings.NewLineOnAttributes = true;
                settings.NamespaceHandling = NamespaceHandling.OmitDuplicates;
                
                using (var xmlWriter = XmlWriter.Create(stringBuilder, settings)) {
                    Swid.Save(xmlWriter);
                }

                return stringBuilder.ToString();
            }
        }
        #endregion
    }

    public class SoftwareIdentityVersionComparer : IComparer<SoftwareIdentity> {
        public static SoftwareIdentityVersionComparer Instance = new SoftwareIdentityVersionComparer();

        public int Compare(SoftwareIdentity x, SoftwareIdentity y) {
            if (x == null || y == null) {
                // can't compare vs null.
                return 0;
            }
            var xVersionScheme = x.VersionScheme ?? string.Empty;
            var yVersionScheme = y.VersionScheme ?? string.Empty;

            if (!x.VersionScheme.EqualsIgnoreCase(yVersionScheme)) {
                // can't compare versions between different version schemes
                return 0;
            }

            var xVersion = x.Version ?? string.Empty;
            var yVersion = y.Version ?? string.Empty;

            switch ((xVersionScheme ?? "unknown").ToLowerInvariant()) {
                case "alphanumeric":
                    // string sort
                    return String.Compare(xVersion, yVersion, StringComparison.Ordinal);
                    
                case "decimal":
                    double xDouble;
                    double yDouble;
                    if (double.TryParse(xVersion, out xDouble) && double.TryParse(yVersion, out yDouble)) {
                        return xDouble.CompareTo(yDouble);
                    }
                    return 0;
                    

                    case "multipartnumeric":
                    return CompareMultipartNumeric(xVersion, yVersion);

                    
                    case "multipartnumeric+suffix":
                    return CompareMultipartNumericSuffix(xVersion, yVersion);
                    
                    case "semver":
                        return CompareSemVer(xVersion, yVersion);

                    case "unknown":
                    // can't sort what we don't know
                        return 0;
                    
                default :
                    // can't sort what we don't know
                    return 0;
            }
        }

        private static int CompareMultipartNumeric(string xVersion, string yVersion) {
            var xs = xVersion.Split('.');
            var ys = yVersion.Split('.');
            for (var i = 0; i < xs.Length; i++) {
                ulong xLong;
                ulong yLong;

                if (ulong.TryParse(xs[i], out xLong) && ulong.TryParse(ys.Length > i ? ys[i] : "0", out yLong)) {
                    var compare = xLong.CompareTo(yLong);
                    if (compare != 0) {
                        return compare;
                    }
                    continue;
                }
                return 0;
            }
            return 0;
        }

        private static int CompareMultipartNumericSuffix(string xVersion, string yVersion) {
            var xPos = IndexOfNotAny(xVersion);
            var yPos = IndexOfNotAny(yVersion);
            var xMulti = xPos == -1 ? xVersion : xVersion.Substring(0, xPos);
            var yMulti = yPos == -1 ? yVersion : yVersion.Substring(0, yPos);
            var compare = CompareMultipartNumeric(xMulti, yMulti);
            if (compare != 0) {
                return compare;
            }

            if (xPos == -1 && yPos == -1) {
                // no suffixes?
                return 0;
            }

            if (xPos == -1) {
                // x has no suffix, y does
                // y is later.
                return -1;
            }

            if (yPos == -1) {
                // x has suffix, y doesn't
                // x is later.
                return 1;
            }

            return String.Compare(xVersion.Substring(xPos), yVersion.Substring(yPos), StringComparison.Ordinal);
        }

        private static int CompareSemVer(string xVersion, string yVersion) {
            var xPos = IndexOfNotAny(xVersion);
            var yPos = IndexOfNotAny(yVersion);
            var xMulti = xPos == -1 ? xVersion : xVersion.Substring(0, xPos);
            var yMulti = yPos == -1 ? yVersion : yVersion.Substring(0, yPos);
            var compare = CompareMultipartNumeric(xMulti, yMulti);
            if (compare != 0) {
                return compare;
            }

            if (xPos == -1 && yPos == -1) {
                // no suffixes?
                return 0;
            }

            if (xPos == -1) {
                // x has no suffix, y does
                // x is later.
                return 1;
            }

            if (yPos == -1) {
                // x has suffix, y doesn't
                // y is later.
                return -1;
            }

            return String.Compare(xVersion.Substring(xPos), yVersion.Substring(yPos), StringComparison.Ordinal);
        }

        private static int IndexOfNotAny(string version, params char[] chars) {
            if (string.IsNullOrEmpty(version)) {
                return -1;
            }
            var n = 0;
            foreach (var ch in version) {
                if (chars.Contains(ch)) {
                    return n;
                }
                n++;
            }
            return -1;
        }
    }
}