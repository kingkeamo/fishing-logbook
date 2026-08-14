# Grafana Cloud Loki write access for diagnostic shipping.
#
# Does NOT create a Grafana Cloud stack. Free accounts include one stack created at
# signup; a second stack can require a paid plan. This module looks up the existing
# stack and creates a logs:write access policy + token for the API.
#
# Destroying the token invalidates Fly / user-secret credentials until they are
# replaced. The stack itself is not managed here.

terraform {
  required_providers {
    grafana = {
      source = "grafana/grafana"
    }
  }
}

locals {
  resource_prefix = "fishing-logbook-${var.environment}"
  logs_base_url   = trimsuffix(data.grafana_cloud_stack.this.logs_url, "/")
  loki_push_url   = endswith(local.logs_base_url, "/loki/api/v1/push") ? local.logs_base_url : "${local.logs_base_url}/loki/api/v1/push"
}

data "grafana_cloud_stack" "this" {
  slug = var.stack_slug
}

resource "grafana_cloud_access_policy" "loki_write" {
  region       = data.grafana_cloud_stack.this.region_slug
  name         = "${local.resource_prefix}-loki-write"
  display_name = "${local.resource_prefix} Loki write"

  scopes = ["logs:write"]

  realm {
    type       = "stack"
    identifier = data.grafana_cloud_stack.this.id
  }
}

resource "grafana_cloud_access_policy_token" "loki_write" {
  region           = grafana_cloud_access_policy.loki_write.region
  access_policy_id = grafana_cloud_access_policy.loki_write.policy_id
  name             = "${local.resource_prefix}-loki-write"
  display_name     = "${local.resource_prefix} Loki write"
}
