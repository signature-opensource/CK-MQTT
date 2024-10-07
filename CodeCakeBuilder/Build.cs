using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace CodeCake;


/// <summary>
/// Standard build "script".
/// </summary>

public partial class Build : CodeCakeHost
{
    public Build()
    {
        Cake.Log.Verbosity = Verbosity.Diagnostic;

        StandardGlobalInfo globalInfo = CreateStandardGlobalInfo()
                                            .AddDotnet()
                                            .SetCIBuildTag();

        Task( "Check-Repository" )
            .Does( () =>
            {
                globalInfo.TerminateIfShouldStop();
            } );

        Task( "Clean" )
            .IsDependentOn( "Check-Repository" )
            .Does( () =>
             {
                 globalInfo.GetDotnetSolution().Clean();
                 Cake.CleanDirectories( globalInfo.ReleasesFolder.ToString() );

             } );


        Task( "Build" )
            .IsDependentOn( "Clean" )
            .IsDependentOn( "Check-Repository" )
            .Does( () =>
             {
                 globalInfo.GetDotnetSolution().Build();
             } );

        Task( "Unit-Testing" )
            .IsDependentOn( "Build" )
            .WithCriteria( () => Cake.InteractiveMode() == InteractiveMode.NoInteraction
                                 || Cake.ReadInteractiveOption( "RunUnitTests", "Run Unit Tests?", 'Y', 'N' ) == 'Y' )
           .Does( () =>
            {
                globalInfo.GetDotnetSolution().SolutionTest();
            } );


        Task( "Create-NuGet-Packages" )
            .WithCriteria( () => globalInfo.IsValid )
            .IsDependentOn( "Unit-Testing" )
            .Does( () =>
             {
                 globalInfo.GetDotnetSolution().Pack();
             } );

        Task( "Push-Packages" )
            .WithCriteria( () => globalInfo.IsValid )
            .IsDependentOn( "Create-NuGet-Packages" )
            .Does( async () =>
             {
                 await globalInfo.PushArtifactsAsync();
             } );

        // The Default task for this script can be set here.
        Task( "Default" )
            .IsDependentOn( "Push-Packages" );

    }

}
