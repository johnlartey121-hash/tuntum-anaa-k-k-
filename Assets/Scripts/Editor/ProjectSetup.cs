using UnityEditor;
using UnityEngine;

namespace Tuntum.EditorTools
{
    /// <summary>
    /// Keeps the player settings in code so the cloud builds (and anyone opening the project) get the same
    /// app name, ids, portrait screen and store-ready options. Runs every time the editor loads.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string Company = "John Lartey";
        public const string Product = "Tuntum anaa Kɔkɔɔ";
        public const string BundleId = "com.johnlartey.tuntum";
        public const string Version = "1.0.0";

        static ProjectSetup() => Apply(); // before any build command runs, including the cloud builds

        [MenuItem("Tuntum/Apply project settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);

            // portrait only, like the prototype
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(1f, .847f, .302f);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = true;

            // Android: Google Play needs 64-bit (IL2CPP + ARM64) and a recent target SDK
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;

            // iOS
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);

            // Web: small download, works on any static host (GitHub Pages, itch.io)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        }
    }
}
