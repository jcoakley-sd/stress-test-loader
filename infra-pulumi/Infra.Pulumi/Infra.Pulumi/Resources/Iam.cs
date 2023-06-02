using Pulumi;
using Aws = Pulumi.Aws;

namespace Infra.Pulumi.Resources;

class Iam : ComponentResource
{
    public Output<string> StressTestClientReadProfileName { get; set; }

    public Iam(StressConfig cfg, ComponentResourceOptions opts) : base("stl:aws:Iam", $"Iam-{cfg.Region}", opts)
    {
      // jcoakley: consider folding the RolePolicy into the Role definition
      // also, consider using Amazon.Auth.AccessControlPolicy's Policy class to construct iam policies
      /*
       *        AssumeRolePolicy = new Policy().WithStatements(
                    new Statement(Statement.StatementEffect.Allow)
                        .WithId(null)
                        .WithActionIdentifiers(new ActionIdentifier("sts:AssumeRole"))
                        .WithPrincipals(new Principal(Principal.SERVICE_PROVIDER, "ec2.amazonaws.com"))
                        .WithConditions(assumeRolePolicyCondition))
                    .ToJson(),
       */
      var stressTestClientReadRole = new Aws.Iam.Role($"stressTestClientReadRole-{cfg.Region}", new Aws.Iam.RoleArgs
        {
            AssumeRolePolicy = @"{
  ""Version"": ""2012-10-17"",
  ""Statement"": [
    {
      ""Action"": ""sts:AssumeRole"",
      ""Principal"": {
        ""Service"": ""ec2.amazonaws.com""
      },
      ""Effect"": ""Allow"",
      ""Sid"": """"
    }
  ]
}
",
            Tags =
            {
                { "tag-key", "stress_test" },
            },
        }, new CustomResourceOptions { Parent = this });

        var stressTestClientReadProfile = new Aws.Iam.InstanceProfile($"stressTestClientReadProfile-{cfg.Region}", new Aws.Iam.InstanceProfileArgs
        {
            Role = stressTestClientReadRole.Name,
        }, new CustomResourceOptions { Parent = this });
        this.StressTestClientReadProfileName = stressTestClientReadProfile.Name;

        var stressTestClientRead = new Aws.Iam.RolePolicy($"stressTestClientRead-{cfg.Region}", new Aws.Iam.RolePolicyArgs
        {
            Role = stressTestClientReadRole.Id,
            // jcoakley: we should create these buckets in pulumi and then reference them here
            Policy = @"{
  ""Version"": ""2012-10-17"",
  ""Statement"": [
    {
      ""Action"": [
        ""s3:GetObject""
      ],
      ""Effect"": ""Allow"",
      ""Resource"": ""arn:aws:s3:::cubestressclientartifactbucket*/*""
    },
    {
      ""Action"": [
        ""s3:PutObject""
      ],
      ""Effect"": ""Allow"",
      ""Resource"": ""arn:aws:s3:::cubestresstest-log/*""
    }
  ]
}
",
        }, new CustomResourceOptions { Parent = this });

        RegisterOutputs();
    }
}

