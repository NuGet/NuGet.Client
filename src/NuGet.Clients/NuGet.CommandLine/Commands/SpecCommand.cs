//using system;
//using system.collections.generic;
//using system.io;
//using system.linq;
//using system.text.regularexpressions;
//using nuget.common;

//namespace nuget.commandline
//{
//    [command(typeof(nugetcommand), "spec", "speccommanddescription", maxargs = 1,
//            usagesummaryresourcename = "speccommandusagesummary", usageexampleresourcename = "speccommandusageexamples")]
//    public class speccommand : command
//    {
//        internal static readonly string sampleprojecturl = "http://project_url_here_or_delete_this_line";
//        internal static readonly string samplelicenseurl = "http://license_url_here_or_delete_this_line";
//        internal static readonly string sampleiconurl = "http://icon_url_here_or_delete_this_line";
//        internal static readonly string sampletags = "tag1 tag2";
//        internal static readonly string samplereleasenotes = "summary of changes made in this release of the package.";
//        internal static readonly string sampledescription = "package description";
//        internal static readonly manifestdependency samplemanifestdependency = new manifestdependency { id = "sampledependency", version = "1.0" };

//        [option(typeof(nugetcommand), "speccommandassemblypathdescription")]
//        public string assemblypath
//        {
//            get;
//            set;
//        }

//        [option(typeof(nugetcommand), "speccommandforcedescription")]
//        public bool force
//        {
//            get;
//            set;
//        }

//        public override void executecommand()
//        {
//            var manifest = new manifest();
//            string projectfile = null;
//            string filename = null;

//            if (!string.isnullorempty(assemblypath))
//            {
//                // extract metadata from the assembly
//                string path = path.combine(currentdirectory, assemblypath);
//                assemblymetadata metadata = assemblymetadataextractor.getmetadata(path);
//                manifest.metadata.id = metadata.name;
//                manifest.metadata.version = metadata.version.tostring();
//                manifest.metadata.authors = metadata.company;
//                manifest.metadata.description = metadata.description;
//            }
//            else
//            {
//                if (!projecthelper.trygetprojectfile(currentdirectory, out projectfile))
//                {
//                    manifest.metadata.id = arguments.any() ? arguments[0] : "package";
//                    manifest.metadata.version = "1.0.0";
//                }
//                else
//                {
//                    filename = path.getfilenamewithoutextension(projectfile);
//                    manifest.metadata.id = "$id$";
//                    manifest.metadata.title = "$title$";
//                    manifest.metadata.version = "$version$";
//                    manifest.metadata.description = "$description$";
//                    manifest.metadata.authors = "$author$";
//                }
//            }

//            // get the file name from the id or the project file
//            filename = filename ?? manifest.metadata.id;

//            // if we're using a project file then we want the a minimal nuspec
//            if (string.isnullorempty(projectfile))
//            {
//                manifest.metadata.description = manifest.metadata.description ?? sampledescription;
//                if (string.isnullorempty(manifest.metadata.authors))
//                {
//                    manifest.metadata.authors = environment.username;
//                }
//                manifest.metadata.dependencysets = new list<manifestdependencyset>();
//                manifest.metadata.dependencysets.add(new manifestdependencyset
//                {
//                    dependencies = new list<manifestdependency> { samplemanifestdependency }
//                });
//            }

//            manifest.metadata.projecturl = sampleprojecturl;
//            manifest.metadata.licenseurl = samplelicenseurl;
//            manifest.metadata.iconurl = sampleiconurl;
//            manifest.metadata.tags = sampletags;
//            manifest.metadata.copyright = "copyright " + datetime.now.year;
//            manifest.metadata.releasenotes = samplereleasenotes;
//            string nuspecfile = filename + constants.manifestextension;

//            // skip the creation if the file exists and force wasn't specified
//            if (file.exists(nuspecfile) && !force)
//            {
//                console.writeline(localizedresourcemanager.getstring("speccommandfileexists"), nuspecfile);
//            }
//            else
//            {
//                try
//                {
//                    using (var stream = new memorystream())
//                    {
//                        manifest.save(stream, validate: false);
//                        stream.seek(0, seekorigin.begin);
//                        string content = stream.readtoend();
//                        file.writealltext(nuspecfile, removeschemanamespace(content));
//                    }

//                    console.writeline(localizedresourcemanager.getstring("speccommandcreatednuspec"), nuspecfile);
//                }
//                catch
//                {
//                    // cleanup the file if it fails to save for some reason
//                    file.delete(nuspecfile);
//                    throw;
//                }
//            }
//        }

//        public override bool includedinhelp(string optionname)
//        {
//            if (string.equals(optionname, "configfile", stringcomparison.ordinalignorecase))
//            {
//                return false;
//            }

//            return base.includedinhelp(optionname);
//        }

//        private static string removeschemanamespace(string content)
//        {
//            // this seems to be the only way to clear out xml namespaces.
//            return regex.replace(content, @"(xmlns:?[^=]*=[""][^""]*[""])", string.empty, regexoptions.ignorecase | regexoptions.multiline);
//        }
//    }
//}
