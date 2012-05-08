using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.Vsip;
using xunit.runner.visualstudio.vs2010.core;
using xunit.runner.visualstudio.vs2010.installer;

namespace Xunit.Runner.VisualStudio.VS2010
{
    /// <summary>
    /// XUnit Test package implementation.
    /// Implement a package when you need to host your own editor, result details viewer, or new test wizard.
    /// Packages are standard VSIP packages and derive from Microsoft.VisualStudio.Shell.Package.
    /// There are a number of attributes to help with registration, including
    /// RegisterTestType, ProvideServiceForTestType, ProvideToolWindow, and AddNewItemTemplates.
    /// </summary>

    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is a package.
    [PackageRegistration(UseManagedResourcesOnly = true, RegisterUsing = RegistrationMethod.CodeBase)]
    [Guid(Guids.PackageKey_S)]

    // This attribute is used to register the informations needed to show the this package in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#101", "#102", "2.0", IconResourceID = 103)]

    [ProvideMenuResource("Menus.ctmenu", 1)] // This attribute is needed to let the shell know that this package exposes some menus.

    // [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string)]

    //[DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\10.0")] // all internal packages seem to have it, but the sample vs package generated by vs did not!

    //[ProvideLoadKey("Professional", "2.0", "xUnit test runner for VS2010", "quetzalcoatl", (short)ResourceIds.TestPackageLoadKey)] - turned off. sample vs package did not have it, legacy thing

    [RegisterTestTypeNoEditor(typeof(XUnitDummyTest), typeof(XUnitTestTip), new string[] { ".dll", ".exe" }, new int[] { (int)ResourceIds.TestIcon, (int)ResourceIds.TestIcon }, (int)ResourceIds.TestName)]

    public sealed class XUnitTestPackage : Package
    {
        /// <value>
        /// The Instance of the package that allows other classess to access the packages services
        /// </value>
        public static XUnitTestPackage Instance { get { Debug.Assert(m_package != null, "Package needs to be created before Instance is called."); return m_package; } } private static XUnitTestPackage m_package;

        static XUnitTestPackage()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var ass = typeof(XUnitTestAdapter).Assembly;
            if (ass.FullName == args.Name)
                // q: currently, I have no idea why some of the internal VS's appdomains can't find the 'core' assembly
                // without manual help, the TIP will not be found -- the strangest part is that the assembly IS LOADED into memory, and into THIS appdomain!
                // sender == AppDomain.Current && System.Array.IndexOf(System.AppDomain.CurrentDomain.GetAssemblies(), ass) != -1 !!!
                return ass;
            return null;
        }

        /// <summary>
        /// Constructor for the package. Setup the required Service and associated callback
        /// </summary>
        public XUnitTestPackage()
        {
            m_package = this;
        }

        private Assembly refs;
        private Assembly[] asms;
        private string vsPath, agentx;

        protected override void Initialize()
        {
            base.Initialize();

            ensureAssembliesInstalled();

            buildUiCommands();
        }

        private void ensureAssembliesInstalled()
        {
            refs = typeof(XUnitTestPackage).Assembly;
            asms = Meta.RequiredAssemblies;
            vsPath = MSVST2A_Access.VisualStudioIdePath;
            agentx = MSVST2A_Access.QTAgentExecutableFilename;

            var assst = AssemblyInstaller.CheckInstalledAssemblies(vsPath, asms);
            var cfgst = ExeConfigPatcher.CheckExeConfigState(vsPath, agentx, AssemblyInstaller.VSPrivateSubDir, asms);
            var regst = RegistryPatcher.CheckHklmTestTypesState();//this.UserRegistryRoot.Name);

            //var ttic = Type.GetType("Microsoft.VisualStudio.TestTools.Common.TestTypeInfoCollection, Microsoft.VisualStudio.QualityTools.Common, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            //var ttic1 = Activator.CreateInstance(ttic);

            ////var reg = Type.GetType("Microsoft.VisualStudio.TestTools.Common.RegistryConstants, Microsoft.VisualStudio.QualityTools.Common, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            ////var vsiphelper = Type.GetType("Microsoft.VisualStudio.TestTools.Vsip.ConfigurationHelper, Microsoft.VisualStudio.QualityTools.Vsip, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            ////var apr1 = reg.GetProperty("ApplicationRoot", BindingFlags.Static | BindingFlags.Public).GetValue(null, null); // !Exp, -HK
            ////var def1 = reg.GetProperty("DefaultRoot", BindingFlags.Static | BindingFlags.Public).GetValue(null, null); // !Exp, -HK

            ////var ttic2 = Activator.CreateInstance(ttic);

            ////vsiphelper.InvokeMember("SetRegistryRoot", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic, null, null, new object[] { this, false });

            ////var apr2 = reg.GetProperty("ApplicationRoot", BindingFlags.Static | BindingFlags.Public).GetValue(null, null); // Exp, -HK
            ////var def2 = reg.GetProperty("DefaultRoot", BindingFlags.Static | BindingFlags.Public).GetValue(null, null); // Exp, -HK

            ////var ttic3 = Activator.CreateInstance(ttic);

            //var testhelper = Type.GetType("Microsoft.VisualStudio.TestTools.Common.TestConfigHelper, Microsoft.VisualStudio.QualityTools.Common, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            //var helper = Activator.CreateInstance(testhelper, true);
            //var defroot = testhelper.GetProperty("LocalMachineConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            //var tmp = defroot.GetValue(helper, null);

            var k = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Default);
            var l = k.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0\\EnterpriseTools\\QualityTools\\TestTypes");
            var m = l.SubKeyCount;

            //var ttic4 = Activator.CreateInstance(ttic);

            if (assst.Values.Contains("missing")
                || assst.Any(asst => !cfgst[asst.Key].Value) // ignore junk
                || !regst
                )
                if (true == new Bounce { Topmost = true }.ShowDialog() && !Zombied)
                {
                    tapAgentProcess();
                    ModuleInstallerWrapper.RunInteractive(true, this.UserLocalDataPath, /*this.UserRegistryRoot.Name,*/ vsPath, agentx, refs, asms);
                }
        }

        private void buildUiCommands()
        {
            // OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            var mcs = GetService(typeof(IMenuCommandService)) as IMenuCommandService;
            if (null != mcs && !Zombied)
            {
                {
                    CommandID menuCommandID = new CommandID(Guids.IDETestToolsKey, (int)PkgCmdIDList.cmdRunModuleManager);
                    MenuCommand menuItem = new MenuCommand(CmdRunModuleManager_Invoke, menuCommandID);
                    mcs.AddCommand(menuItem);
                }
                {
                    CommandID menuCommandID = new CommandID(Guids.IDETestToolsKey, (int)PkgCmdIDList.cmdTerminateAgent);
                    MenuCommand menuItem = new MenuCommand(CmdTerminateAgent_Invoke, menuCommandID);
                    mcs.AddCommand(menuItem);
                }
                {
                    CommandID menuCommandID = new CommandID(Guids.ProjectToolsKey, (int)PkgCmdIDList.cmdOpenProjectTypeEditor);
                    OleMenuCommand menuItem = new OleMenuCommand(CmdOpenProjectTypeEditor_Invoke, CmdOpenProjectTypeEditor_StatusChanged, CmdOpenProjectTypeEditor_QueryStatus, menuCommandID);
                    mcs.AddCommand(menuItem);
                }
            }
        }

        //---------------------------------

        private void CmdRunModuleManager_Invoke(object sender, EventArgs e)
        {
            if (Zombied) return;

            tapAgentProcess();
            ModuleInstallerWrapper.RunInteractive(true, this.UserLocalDataPath, vsPath, agentx, refs, asms);
        }

        //---------------------------------

        private void CmdTerminateAgent_Invoke(object sender, EventArgs e)
        {
            if (Zombied) return;

            tapAgentProcess();
        }

        private void tapAgentProcess()
        {
            var tmi = MSVST3M_Access.GetTmi(this);
            MSVST3M_Access.ShutdownLocalAgent(tmi);
        }

        //---------------------------------

        private void CmdOpenProjectTypeEditor_StatusChanged(object sender, EventArgs e)
        {
            // called when command's status changes
        }
        private void CmdOpenProjectTypeEditor_QueryStatus(object sender, EventArgs e)
        {
            if (Zombied) return;

            OleMenuCommand menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                IntPtr hierarchyPtr, selectionContainerPtr;
                uint projectItemId;
                IVsMultiItemSelect mis;
                IVsMonitorSelection monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
                monitorSelection.GetCurrentSelection(out hierarchyPtr, out projectItemId, out mis, out selectionContainerPtr);

                IVsHierarchy hierarchy = Marshal.GetTypedObjectForIUnknown(hierarchyPtr, typeof(IVsHierarchy)) as IVsHierarchy;
                var proagg = hierarchy as IVsAggregatableProject;
                if (proagg != null)
                {
                    string kinds_ = null;
                    ErrorHandler.ThrowOnFailure(proagg.GetAggregateProjectTypeGuids(out kinds_));
                    var kinds = kinds_.Split(';').Select(s => s.ToUpper()).ToList();

                    menuCommand.Text = kinds.Contains("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}") ? "Disable UnitTest extension" : "Enable UnitTest extension";
                }
            }
        }
        private void CmdOpenProjectTypeEditor_Invoke(object sender, EventArgs e)
        {
            if (Zombied) return;

            // http://social.msdn.microsoft.com/Forums/en/vsx/thread/80ca5bbb-2475-4cf7-a74d-c0bbcbbf6946
            //EnvDTE.DTE dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            //if (dte == null)
            //    return;

            uint projectItemId;
            IVsHierarchy vsHierarchyProject = getCurrentVSHierarchySelection(out projectItemId);
            if (vsHierarchyProject == null)
                return;

            var project = ToDteProject(vsHierarchyProject);
            if (project == null)
                return;

            // http://www.mztools.com/articles/2008/MZ2008017.aspx
            var vss = GetService(typeof(IVsSolution)) as IVsSolution; // -> sol4 = reload project
            var vss4 = vss as IVsSolution4;

            var proagg = vsHierarchyProject as IVsAggregatableProject;
            string kinds_ = null;
            if (proagg != null)
            {
                ErrorHandler.ThrowOnFailure(proagg.GetAggregateProjectTypeGuids(out kinds_));
                var kinds = kinds_.Split(';').Select(s => s.ToUpper()).ToList();
                if (!kinds.Contains("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}"))
                {
                    kinds.Insert(0, "{3AC096D0-A1C2-E12C-1390-A8335801FDAB}"); // the normal project type must be the LAST of the guids
                    ErrorHandler.ThrowOnFailure(proagg.SetAggregateProjectTypeGuids(string.Join(";", kinds)));
                }
                else
                {
                    kinds.Remove("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}"); ;
                    ErrorHandler.ThrowOnFailure(proagg.SetAggregateProjectTypeGuids(string.Join(";", kinds)));
                }

                // http://www.pablogaliano.com/2008/08/how-do-i-obtain-project-guid.html
                Guid projGuid;
                ErrorHandler.ThrowOnFailure(vsHierarchyProject.GetGuidProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out projGuid));
                ErrorHandler.ThrowOnFailure(vss4.ReloadProject(projGuid));
            }
        }
        public IVsHierarchy getCurrentVSHierarchySelection(out uint projectItemId)
        {
            IntPtr hierarchyPtr, selectionContainerPtr;
            projectItemId = 0;
            IVsMultiItemSelect mis;
            IVsMonitorSelection monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
            monitorSelection.GetCurrentSelection(out hierarchyPtr, out projectItemId, out mis, out selectionContainerPtr);

            IVsHierarchy hierarchy = Marshal.GetTypedObjectForIUnknown(hierarchyPtr, typeof(IVsHierarchy)) as IVsHierarchy;
            return hierarchy;
        }
        public EnvDTE.Project ToDteProject(IVsHierarchy hierarchy)
        {
            if (hierarchy == null) throw new ArgumentNullException("hierarchy");
            object prjObject = null;
            if (hierarchy.GetProperty(0xfffffffe, (int)__VSHPROPID.VSHPROPID_ExtObject, out prjObject) == VSConstants.S_OK)
                return (EnvDTE.Project)prjObject;
            else
                throw new ArgumentException("Hierarchy is not a project.");
        }
    }
}
