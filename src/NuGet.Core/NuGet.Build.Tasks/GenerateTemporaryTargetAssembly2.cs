
//---------------------------------------------------------------------------
//
// <copyright file="GenerateTemporaryTargetAssembly.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
// 
// Description: This is a MSBuild task which generates a temporary target assembly
//              if current project contains a xaml file with local-type-reference.
//
//              It generates a temporary project file and then call build-engine 
//              to compile it.
//              
//              The new project file will replace all the Reference Items with the 
//              resolved ReferenctPath, add all the generated code file into Compile 
//              Item list.
//
// History:
//  05/10/05: weibz   Created.
//  11/03/17: support NuGet PackageReference properly in temporary assembly by enabling .g.targets and .g.props to be
//            properly included, despite the project file name change.
//
//---------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections;

using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.BuildEngine;

using MS.Utility;
using MS.Internal.Tasks;

// Since we disable PreSharp warnings in this file, PreSharp warning is unknown to C# compiler.
// We first need to disable warnings about unknown message numbers and unknown pragmas.
#pragma warning disable 1634, 1691

namespace Microsoft.Build.Tasks.Windows
{
    #region GenerateTemporaryTargetAssembly2 Task class

    /// <summary>
    ///   This task is used to generate a temporary target assembly. It generates
    ///   a temporary project file and then compile it.
    /// 
    ///   The generated project file is based on current project file, with below
    ///   modification:
    /// 
    ///       A:  Add the generated code files (.g.cs) to Compile Item list.
    ///       B:  Replace Reference Item list with ReferenctPath item list.
    ///           So that it doesn't need to rerun time-consuming tatk 
    ///           ResolveAssemblyReference (RAR) again.
    /// 
    /// </summary>
    public sealed class GenerateTemporaryTargetAssembly2 : Task
    {
        //------------------------------------------------------
        //
        //  Constructors
        //
        //------------------------------------------------------

        #region Constructors

        /// <summary>
        /// Constrcutor
        /// </summary>
        public GenerateTemporaryTargetAssembly2()
        {
        }

        #endregion Constructors

        //------------------------------------------------------
        //
        //  Public Methods
        //
        //------------------------------------------------------

        #region Public Methods

        /// <summary>
        /// ITask Execute method
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool retValue = true;

            // Verification
            try
            {
                XmlDocument xmlProjectDoc = null;

                xmlProjectDoc = new XmlDocument();
                xmlProjectDoc.Load(CurrentProject);

                //
                // remove all the WinFX specific item lists
                // ApplicationDefinition, Page, MarkupResource and Resource
                //

                RemoveItemsByName(xmlProjectDoc, APPDEFNAME);
                RemoveItemsByName(xmlProjectDoc, PAGENAME);
                RemoveItemsByName(xmlProjectDoc, MARKUPRESOURCENAME);
                RemoveItemsByName(xmlProjectDoc, RESOURCENAME);

                // Replace the Reference Item list with ReferencePath

                RemoveItemsByName(xmlProjectDoc, REFERENCETYPENAME);
                AddNewItems(xmlProjectDoc, ReferencePathTypeName, ReferencePath);

                // Add GeneratedCodeFiles to Compile item list.
                AddNewItems(xmlProjectDoc, CompileTypeName, GeneratedCodeFiles);


                // Create a random file name
                // This can fix the problem of project cache in VS.NET environment.
                //

                // GetRandomFileName( ) could return any possible file name and extension, but
                // some file extension has special meaning in MSBUILD system, such as a ".sln"
                // means the file is a solution file with special file format. Since the temporary
                // file is just for a project, we can use a fixed extension here, but the basic 
                // file name is still random which can fix above VS.NET bug.
                //
                string randProjPath = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(CurrentProject));

                // Save the xmlDocument content into the temporary project file.
                xmlProjectDoc.Save(randProjPath);

                //
                // Invoke MSBUILD engine to build this temporary project file.
                //

                Hashtable globalProperties = new Hashtable(3);

                // Add AssemblyName and IntermediateOutputPath to the global property list
                globalProperties[intermediateOutputPathPropertyName] = IntermediateOutputPath;
                globalProperties[assemblyNamePropertyName] = AssemblyName;
                globalProperties[originalProjectNamePropertyName] = Path.GetFileNameWithoutExtension(CurrentProject);

                retValue = BuildEngine.BuildProjectFile(randProjPath, new string[] { CompileTargetName }, globalProperties, null);

                try
                {
                    // Delete the random project file from disk unless an advanced user wants to keep it around to help debug failures.
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVarWPFDoNotDeleteTemporaryProject)))
                    {
                        File.Delete(randProjPath);
                    }
                }
                catch (IOException e)
                {
                    // Failure to delete the file is a non fatal error
                    Log.LogWarningFromException(e);
                }

            }
#pragma warning disable 6500
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                retValue = false;
            }
#pragma warning restore 6500

            return retValue;
        }
        #endregion Public Methods

        //------------------------------------------------------
        //
        //  Public Properties
        //
        //------------------------------------------------------

        #region Public Properties

        /// <summary>
        /// CurrentProject 
        ///    The full path of current project file.
        /// </summary>
        [Required]
        public string CurrentProject
        {
            get { return _currentProject; }
            set { _currentProject = value; }
        }

        /// <summary>
        /// MSBuild Binary Path.
        ///   This is required for Project to work correctly.
        /// </summary>
        [Required]
        public string MSBuildBinPath
        {
            get { return _msbuildBinPath; }
            set { _msbuildBinPath = value; }
        }

        /// <summary>
        /// GeneratedCodeFiles
        ///    A list of generated code files, it could be empty.
        ///    The list will be added to the Compile item list in new generated project file.
        /// </summary>
        public ITaskItem[] GeneratedCodeFiles
        {
            get { return _generatedCodeFiles; }
            set { _generatedCodeFiles = value; }
        }

        /// <summary>
        /// CompileTypeName
        ///   The appropriate item name which can be accepted by managed compiler task.
        ///   It is "Compile" for now.
        ///   
        ///   Adding this property is to make the type name configurable, if it is changed, 
        ///   No code is required to change in this task, but set a new type name in project file.
        /// </summary>
        [Required]
        public string CompileTypeName
        {
            get { return _compileTypeName; }
            set { _compileTypeName = value; }
        }


        /// <summary>
        /// ReferencePath
        ///    A list of resolved reference assemblies.
        ///    The list will replace the original Reference item list in generated project file.
        /// </summary>
        public ITaskItem[] ReferencePath
        {
            get { return _referencePath; }
            set { _referencePath = value; }
        }

        /// <summary>
        /// ReferencePathTypeName
        ///   The appropriate item name which is used to keep the Reference list in managed compiler task.
        ///   It is "ReferencePath" for now.
        ///   
        ///   Adding this property is to make the type name configurable, if it is changed, 
        ///   No code is required to change in this task, but set a new type name in project file.
        /// </summary>
        [Required]
        public string ReferencePathTypeName
        {
            get { return _referencePathTypeName; }
            set { _referencePathTypeName = value; }
        }


        /// <summary>
        /// IntermediateOutputPath
        /// 
        /// The value which is set to IntermediateOutputPath property in current project file.
        /// 
        /// Passing this value explicitly is to make sure to generate temporary target assembly 
        /// in expected directory.  
        /// </summary>
        [Required]
        public string IntermediateOutputPath
        {
            get { return _intermediateOutputPath; }
            set { _intermediateOutputPath = value; }
        }

        /// <summary>
        /// AssemblyName
        /// 
        /// The value which is set to AssemblyName property in current project file.
        /// Passing this value explicitly is to make sure to generate the expected 
        /// temporary target assembly.
        /// 
        /// </summary>
        [Required]
        public string AssemblyName
        {
            get { return _assemblyName; }
            set { _assemblyName = value; }
        }

        /// <summary>
        /// CompileTargetName
        /// 
        /// The msbuild target name which is used to generate assembly from source code files.
        /// Usually it is "CoreCompile"
        /// 
        /// </summary>
        [Required]
        public string CompileTargetName
        {
            get { return _compileTargetName; }
            set { _compileTargetName = value; }
        }

        #endregion Public Properties

        //------------------------------------------------------
        //
        //  Private Methods
        //
        //------------------------------------------------------

        #region Private Methods

        //
        // Remove specific items from project file. Every item should be under an ItemGroup.
        //
        private void RemoveItemsByName(XmlDocument xmlProjectDoc, string sItemName)
        {
            if (xmlProjectDoc == null || String.IsNullOrEmpty(sItemName))
            {
                // When the parameters are not valid, simply return it, instead of throwing exceptions.
                return;
            }

            //
            // The project file format is always like below:
            //
            //  <Project  xmlns="...">
            //     <ProjectGroup>
            //         ......
            //     </ProjectGroup>
            //
            //     ...
            //     <ItemGroup>
            //         <ItemNameHere ..../>
            //         ....
            //     </ItemGroup>
            //
            //     ....
            //     <Import ... />
            //     ...
            //     <Target Name="xxx" ..../>
            //     
            //      ...
            //
            //  </Project>
            //
            //
            // The order of children nodes under Project Root element is random
            //

            XmlNode root = xmlProjectDoc.DocumentElement;

            if (root.HasChildNodes == false)
            {
                // If there is no child element in this project file, just return immediatelly.
                return;
            }

            for (int i = 0; i < root.ChildNodes.Count; i++)
            {
                XmlElement nodeGroup = root.ChildNodes[i] as XmlElement;

                if (nodeGroup != null && String.Compare(nodeGroup.Name, ITEMGROUP_NAME, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    //
                    // This is ItemGroup element.
                    //
                    if (nodeGroup.HasChildNodes)
                    {
                        ArrayList itemToRemove = new ArrayList();

                        for (int j = 0; j < nodeGroup.ChildNodes.Count; j++)
                        {
                            XmlElement nodeItem = nodeGroup.ChildNodes[j] as XmlElement;

                            if (nodeItem != null && String.Compare(nodeItem.Name, sItemName, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                // This is the item that need to remove.
                                // Add it into the temporary array list.
                                // Don't delete it here, since it would affect the ChildNodes of parent element.
                                //
                                itemToRemove.Add(nodeItem);
                            }
                        }

                        //
                        // Now it is the right time to delete the elements.
                        //
                        if (itemToRemove.Count > 0)
                        {
                            foreach (object node in itemToRemove)
                            {
                                XmlElement item = node as XmlElement;

                                //
                                // Remove this item from its parent node.
                                // the parent node should be nodeGroup.
                                //
                                if (item != null)
                                {
                                    nodeGroup.RemoveChild(item);
                                }
                            }
                        }
                    }

                    //
                    // Removed all the items with given name from this item group.
                    //
                    // Continue the loop for the next ItemGroup.
                }

            }   // end of "for i" statement.

        }

        //
        // Add a list of files into an Item in the project file, the ItemName is specified by sItemName.
        //
        private void AddNewItems(XmlDocument xmlProjectDoc, string sItemName, ITaskItem[] pItemList)
        {
            if (xmlProjectDoc == null || String.IsNullOrEmpty(sItemName) || pItemList == null)
            {
                // When the parameters are not valid, simply return it, instead of throwing exceptions.
                return;
            }

            XmlNode root = xmlProjectDoc.DocumentElement;

            // Create a new ItemGroup element
            XmlNode nodeItemGroup = xmlProjectDoc.CreateElement(ITEMGROUP_NAME, root.NamespaceURI);

            // Append this new ItemGroup item into the list of children of the document root.
            root.AppendChild(nodeItemGroup);

            XmlElement embedItem = null;

            for (int i = 0; i < pItemList.Length; i++)
            {
                // Create an element for the given sItemName
                XmlElement nodeItem = xmlProjectDoc.CreateElement(sItemName, root.NamespaceURI);

                // Create an Attribute "Include"
                XmlAttribute attrInclude = xmlProjectDoc.CreateAttribute(INCLUDE_ATTR_NAME);

                ITaskItem pItem = pItemList[i];

                // Set the value for Include attribute.
                attrInclude.Value = pItem.ItemSpec;

                // Add the attribute to current item node.
                nodeItem.SetAttributeNode(attrInclude);

                if (TRUE == pItem.GetMetadata(EMBEDINTEROPTYPES))
                {
                    embedItem = xmlProjectDoc.CreateElement(EMBEDINTEROPTYPES, root.NamespaceURI);
                    embedItem.InnerText = TRUE;
                    nodeItem.AppendChild(embedItem);
                }

                string aliases = pItem.GetMetadata(ALIASES);
                if (!String.IsNullOrEmpty(aliases))
                {
                    embedItem = xmlProjectDoc.CreateElement(ALIASES, root.NamespaceURI);
                    embedItem.InnerText = aliases;
                    nodeItem.AppendChild(embedItem);
                }

                // Add current item node into the children list of ItemGroup
                nodeItemGroup.AppendChild(nodeItem);
            }
        }

        #endregion Private Methods


        //------------------------------------------------------
        //
        //  Private Fields
        //
        //------------------------------------------------------

        #region Private Fields

        private string _currentProject = String.Empty;

        private ITaskItem[] _generatedCodeFiles;
        private ITaskItem[] _referencePath;

        private string _referencePathTypeName;
        private string _compileTypeName;

        private string _msbuildBinPath;

        private string _intermediateOutputPath;
        private string _assemblyName;
        private string _compileTargetName;

        private const string intermediateOutputPathPropertyName = "IntermediateOutputPath";
        private const string assemblyNamePropertyName = "AssemblyName";
        private const string originalProjectNamePropertyName = "_OriginalProjectName";

        private const string envVarWPFDoNotDeleteTemporaryProject = "WPF_DoNotDeleteTemporaryProject";

        private const string ALIASES = "Aliases";
        private const string REFERENCETYPENAME = "Reference";
        private const string EMBEDINTEROPTYPES = "EmbedInteropTypes";
        private const string APPDEFNAME = "ApplicationDefinition";
        private const string PAGENAME = "Page";
        private const string MARKUPRESOURCENAME = "MarkupResource";
        private const string RESOURCENAME = "Resource";

        private const string ITEMGROUP_NAME = "ItemGroup";
        private const string INCLUDE_ATTR_NAME = "Include";

        private const string TRUE = "True";

        #endregion Private Fields

    }

    #endregion GenerateProjectForLocalTypeReference Task class
}
