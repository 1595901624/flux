using System.Runtime.InteropServices;

namespace Flux.Services;

internal static class PackageIdentity
{
    public static bool IsPackaged
    {
        get
        {
            try
            {
                _ = Windows.ApplicationModel.Package.Current.Id.Name;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
        }
    }
}
