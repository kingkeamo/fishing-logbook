output "bucket_name" {
  description = "R2 bucket name for catch photographs."
  value       = cloudflare_r2_bucket.photos.name
}

output "location" {
  description = "R2 location hint used when the bucket was created."
  value       = cloudflare_r2_bucket.photos.location
}
