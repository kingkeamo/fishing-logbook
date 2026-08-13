output "project_name" {
  description = "Neon project name for this environment."
  value       = neon_project.this.name
}

output "project_id" {
  description = "Neon project ID."
  value       = neon_project.this.id
}

output "database_name" {
  description = "Default database name."
  value       = neon_project.this.database_name
}

output "database_host" {
  description = "Default database host."
  value       = neon_project.this.database_host
}

output "database_user" {
  description = "Default database role."
  value       = neon_project.this.database_user
}

output "default_branch_id" {
  description = "Default branch ID."
  value       = neon_project.this.default_branch_id
}

output "connection_uri" {
  description = "Default connection URI. Contains credentials — do not commit or log. Fly secrets remain the API source of truth."
  value       = neon_project.this.connection_uri
  sensitive   = true
}
