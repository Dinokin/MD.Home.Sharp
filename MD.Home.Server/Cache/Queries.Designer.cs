﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MD.Home.Server.Cache {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Queries {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Queries() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("MD.Home.Server.Cache.Queries", typeof(Queries).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT AVG(size) FROM cache_entries;.
        /// </summary>
        internal static string AverageSizeOfContents {
            get {
                return ResourceManager.GetString("AverageSizeOfContents", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &gt;.
        /// </summary>
        internal static string CreateDatabase {
            get {
                return ResourceManager.GetString("CreateDatabase", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DELETE FROM cache_entries WHERE id IN (SELECT id FROM cache_entries ORDER BY last_accessed LIMIT $amount);.
        /// </summary>
        internal static string DeleteLeastAccessedEntries {
            get {
                return ResourceManager.GetString("DeleteLeastAccessedEntries", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT id, content_type, last_modified, last_accessed, size, content FROM cache_entries WHERE id = $id;.
        /// </summary>
        internal static string GetEntryById {
            get {
                return ResourceManager.GetString("GetEntryById", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT INTO cache_entries (id, content_type, last_modified, last_accessed, size, content) VALUES ($id, $content_type, $last_modified, $last_accessed, $size, $content);.
        /// </summary>
        internal static string InsertEntry {
            get {
                return ResourceManager.GetString("InsertEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT SUM(size) FROM cache_entries;.
        /// </summary>
        internal static string TotalSizeOfContents {
            get {
                return ResourceManager.GetString("TotalSizeOfContents", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to PRAGMA wal_checkpoint(PASSIVE);.
        /// </summary>
        internal static string TriggerCheckpoint {
            get {
                return ResourceManager.GetString("TriggerCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UPDATE cache_entries SET last_accessed = $last_accessed WHERE id = $id;.
        /// </summary>
        internal static string UpdateEntryLastAccessDate {
            get {
                return ResourceManager.GetString("UpdateEntryLastAccessDate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to VACUUM main;.
        /// </summary>
        internal static string VacuumDatabase {
            get {
                return ResourceManager.GetString("VacuumDatabase", resourceCulture);
            }
        }
    }
}
