using System.ComponentModel.DataAnnotations;
using Infra.Pulumi.Resources;
using McMaster.Extensions.CommandLineUtils;
using Pulumi;
using Pulumi.Automation;
using Pulumi.Aws;
using Config = Pulumi.Config;

#pragma warning disable CS1998

namespace Infra.Pulumi;

[Command("deploy", Description = "Deploy Pulumi stack")]
public class DeployPulumiCommand
{
    public static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<DeployPulumiCommand>(args);

    #region Command Options
    [Option("--destroy",
        "Destroy stack (optional)",
        CommandOptionType.NoValue)]
    private bool Destroy { get; set; }

    [Option("--no-refresh",
        "Do not refresh state store with current live state",
        CommandOptionType.NoValue)]
    private bool NoRefresh { get; set; }

    [Option("--preview",
        "Only preview changes, but do not make them",
        CommandOptionType.NoValue)]
    private bool Preview { get; set; }

    [Required]
    [Option("--project-name",
        "The project name in pulumi",
        CommandOptionType.SingleValue)]
    private string ProjectName { get; set; } = null!;

    [Required]
    [Option("--stack-name",
        "The stack name in pulumi",
        CommandOptionType.SingleValue)]
    private string StackName { get; set; } = null!;
    #endregion

    public async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
    {
        var optimizationVars = new Dictionary<string, string>
        {
            {"PULUMI_EXPERIMENTAL", "1"},
            {"PULUMI_SKIP_CHECKPOINTS", "true"},
            {"PULUMI_OPTIMIZED_CHECKPOINT_PATCH", "true"},
        };
        foreach (var (k,v) in optimizationVars)
        {
            Environment.SetEnvironmentVariable(k, v);
        }

        // jcoakley: consider prompting the user for these env vars if they're not found, or exiting with a warning about which specific vars are missing
        // also consider renaming the variables to be UPPER_CASE_WITH_UNDERSCORES and have a consistent prefix, e.g. STRESS_TEST_LOADER_PUBLIC_KEY, STRESS_TEST_LOADER_REGIONS, etc
        var localPublicIp = Environment.GetEnvironmentVariable("stress_test_loader_allowed_cidr");
        var publicKey = Environment.GetEnvironmentVariable("public_key");

        var desiredCapacity = Environment.GetEnvironmentVariable("desired_capacity") ?? "2";
        var regions = Environment.GetEnvironmentVariable("regions") ?? "us-west-2";

        var cfg = new StressConfig
        {
            Environment = ProjectName,
            DesiredCapacity = int.Parse(desiredCapacity),
            PublicKey = publicKey,
            AllowedCidrBlocks = localPublicIp.Split(",").ToList(),
        };

        #region Deploy
        var program = PulumiFn.Create(async () =>
        {
            var regionList = regions.Split(',').ToList();

            foreach (var region in regionList)
            {
                var provider = new Provider(region, new()
                {
                    Region = region,
                });
                cfg.Region = region;

                var ami = new Ami(cfg, new ComponentResourceOptions { Provider = provider });
                var iam = new Iam(cfg, new ComponentResourceOptions { Provider = provider });
                var vpc = new Vpc(cfg, new ComponentResourceOptions { Provider = provider });
                var autoscaling = new Autoscaling(cfg, ami.AmiId, iam.StressTestClientReadProfileName,
                    vpc.MainVpcId, vpc.MainSubnetIds, new ComponentResourceOptions { Provider = provider });
            }
        });

        if (Destroy)
        {
            return await DeployHelpers.DestroyPulumiAsync(program, ProjectName, StackName, NoRefresh, cancellationToken);
        }

        if (Preview)
        {
            return await DeployHelpers.PreviewPulumiAsync(program, ProjectName, StackName, NoRefresh, cancellationToken);
        }

        return await DeployHelpers.UpdatePulumiAsync(program, ProjectName, StackName, NoRefresh, cancellationToken);
        #endregion
    }
}


