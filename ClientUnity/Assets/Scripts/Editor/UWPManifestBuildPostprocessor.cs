using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Xml;
using System.IO;
using System.Text;


public static class InProcServerExtensions
{
    public static InProcServerExtension[] extensionDefinitions =
    {
        new InProcServerExtension()
        {
            dllName = "WebRtcScheme.dll",
            activatableClasses = new string[]
            {
                "WebRtcScheme.SchemeHandler"
            },
        }
    };
}

#region Extension classes definitions
public struct InProcServerExtension
{
    public string dllName;
    public string[] activatableClasses;
}
#endregion 

[InitializeOnLoad]
public class UWPManifestBuildPostprocessor
{
    [PostProcessBuildAttribute(1024)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WSAPlayer)
            return;

        Debug.Log("Post-processing Universal Windows Platform build...");
        Debug.Log(pathToBuiltProject);

        var manifestPath = pathToBuiltProject + "\\" + UnityEditor.PlayerSettings.productName + "\\Package.appxmanifest";
        manifestPath = manifestPath.Replace('/', '\\');

        if (!File.Exists(manifestPath))
            return;

        XmlDocument doc = new XmlDocument();
        try
        {
            doc.Load(manifestPath);
        }
        catch(Exception ex)
        {
            Debug.LogErrorFormat("Error loading manifest XML: {0}", ex.Message);
            return;
        }
        XmlNode root = doc.DocumentElement;
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);

        nsmgr.AddNamespace("def", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
        XmlNode extensions = root.SelectSingleNode(
             "./def:Extensions", nsmgr);

        if(extensions == null)
        {
            var element = doc.CreateElement("Extensions", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            extensions = root.AppendChild(element);
        }

        foreach (var extDef in InProcServerExtensions.extensionDefinitions)
        {

            var extension = extensions.SelectSingleNode("./def:Extension/def:InProcessServer/def:Path[text()='" + extDef.dllName + "']", nsmgr);
            bool bNewNode = false;

            if (extension == null)
            {
                var element = doc.CreateElement("Extension", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
                var attribute = doc.CreateAttribute("Category");
                attribute.Value = "windows.activatableClass.inProcessServer";
                element.Attributes.Append(attribute);

                extension = element;
                bNewNode = true;
            }
            else
            {
                extension = extension.ParentNode.ParentNode; // path -> InProcessServer->Extension
            }

            StringBuilder builder = new StringBuilder();
            foreach (var c in extDef.activatableClasses)
            {
                builder.AppendFormat("<ActivatableClass ActivatableClassId=\"{0}\" ThreadingModel=\"both\" />\n", c);
            }

            extension.InnerXml = string.Format("<InProcessServer><Path>{0}</Path>{1}</InProcessServer>", extDef.dllName, builder.ToString());

            if (bNewNode)
                extensions.AppendChild(extension);
        }

        doc.LoadXml(doc.OuterXml.Replace("xmlns=\"\"", ""));

        doc.Save(manifestPath);

        Debug.Log("Done post-processing Universal Windows Platform build.");
    }


}
