output "project_name" {
  description = "Cloudflare Pages project name."
  value       = cloudflare_pages_project.web.name
}

output "subdomain" {
  description = "Cloudflare Pages subdomain (typically <name>.pages.dev)."
  value       = cloudflare_pages_project.web.subdomain
}

output "domains" {
  description = "Custom domains attached to the Pages project."
  value       = sort([for domain in cloudflare_pages_domain.this : domain.name])
}
