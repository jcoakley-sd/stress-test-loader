terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 3"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.1.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.2.0"
    }
  }

  required_version = "~> 1.0"

  backend "s3" {
    bucket = "sd-probes-terraform-backend"
    key    = "terraform"
    region = "us-west-2"
  }
}


locals {
  PNS_version = var.PNS_version
}

provider "aws" {
  region  = "us-east-1"
  alias   = "us-east-1"
  profile = "cube-nvprod-bd"
}

provider "aws" {
  region  = "us-west-2"
  alias   = "us-west-2"
  profile = "cube-nvprod-bd"
}

module "iam" {
   source             = "../../terraform-modules/iam"
   environment        = var.environment
   providers = {
    aws = aws.us-east-1
  }
}

module "us-east-1-network" {
  source     = "../../terraform-modules/vpc"
  cidr_block = var.ntw_cidr_block
  az_count   = var.az_count
  providers = {
    aws = aws.us-east-1
  }
}

module "us-west-2-network" {
  source     = "../../terraform-modules/vpc"
  cidr_block = var.ntw_cidr_block
  az_count   = var.az_count
  providers = {
    aws = aws.us-west-2
  }
}

module "ami-us-east-1" {
  source        = "../../terraform-modules/ami"
  source_ami_id = var.source_ami_id
  providers = {
    aws = aws.us-east-1
  }
}

module "ami-us-west-2" {
  source        = "../../terraform-modules/ami"
  source_ami_id = var.source_ami_id
  providers = {
    aws = aws.us-west-2
  }
}
locals {
  user_data = templatefile(join("/", tolist([path.module, "user_data.sh"])), {
    stress_test_loader_allowed_cidr = var.stress_test_loader_allowed_cidr
    stress_test_loader_port         = var.stress_test_loader_port
    environment        = var.environment
  })
}

module "autoscale-us-east-1" {
  source                  = "../../terraform-modules/autoscale"
  vpc_id                  = module.us-east-1-network.aws_vpc_id
 iam_name = module.iam.iam_name
  cidr_block              = var.ntw_cidr_block
  subnet_ids              = module.us-east-1-network.aws_subnet_list
  aws_subnets             = module.us-east-1-network.aws_subnets
  min_size                = var.asg_min
  max_size                = var.asg_max
  desired_capacity        = var.asg_desired
  instance_type           = var.instance_type
  environment             = var.environment
  stress_test_loader_port              = var.stress_test_loader_port
  PNS_version             = local.PNS_version
  down_scaling_adjustment = -(var.asg_min / 2)
  up_scaling_adjustment   = var.asg_min / 2
  domain_rand             = ""
  user_data               = local.user_data
  stress_test_loader_allowed_cidr      = var.stress_test_loader_allowed_cidr
  aws_ami_id              = module.ami-us-east-1.ami_id
  dns_name                = "us-east-1"
  providers = {
    aws = aws.us-east-1
  }
  extra_tags = {
    "type" = "stress_test_loader"
  }
}

module "autoscale-us-west-2" {
  source                  = "../../terraform-modules/autoscale"
  vpc_id                  = module.us-west-2-network.aws_vpc_id
 iam_name = module.iam.iam_name
  cidr_block              = var.ntw_cidr_block
  subnet_ids              = module.us-west-2-network.aws_subnet_list
  aws_subnets             = module.us-west-2-network.aws_subnets
  min_size                = var.asg_min
  max_size                = var.asg_max
  desired_capacity        = var.asg_desired
  instance_type           = var.instance_type
  environment             = var.environment
  stress_test_loader_port              = var.stress_test_loader_port
  PNS_version             = local.PNS_version
  down_scaling_adjustment = -(var.asg_min / 2)
  up_scaling_adjustment   = var.asg_min / 2
  domain_rand             = ""
  user_data               = local.user_data
  stress_test_loader_allowed_cidr      = var.stress_test_loader_allowed_cidr
  aws_ami_id              = module.ami-us-west-2.ami_id
  dns_name                = "us-west-2"
  providers = {
    aws = aws.us-west-2
  }
  extra_tags = {
    "type" = "stress_test_loader"
  }
}


