# Pre Token Generation Lambda (event version V2_0) copies the verified email user
# attribute onto the access token. Cognito access tokens omit email by default.
# Essentials-tier pools support V2_0. Do not log PII. JWT validation is unchanged.

data "archive_file" "pre_token_generation" {
  type        = "zip"
  source_file = "${path.module}/lambda/index.mjs"
  output_path = "${path.module}/.build/pre-token-generation.zip"
}

data "aws_iam_policy_document" "pre_token_generation_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "pre_token_generation" {
  name               = "${local.resource_prefix}-pre-token-generation"
  assume_role_policy = data.aws_iam_policy_document.pre_token_generation_assume.json

  tags = {
    Environment = var.environment
    Name        = "${local.resource_prefix}-pre-token-generation"
  }
}

resource "aws_iam_role_policy_attachment" "pre_token_generation_logs" {
  role       = aws_iam_role.pre_token_generation.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_cloudwatch_log_group" "pre_token_generation" {
  name              = "/aws/lambda/${local.resource_prefix}-pre-token-generation"
  retention_in_days = 7

  tags = {
    Environment = var.environment
    Name        = "${local.resource_prefix}-pre-token-generation"
  }
}

resource "aws_lambda_function" "pre_token_generation" {
  filename         = data.archive_file.pre_token_generation.output_path
  source_code_hash = data.archive_file.pre_token_generation.output_base64sha256
  function_name    = "${local.resource_prefix}-pre-token-generation"
  role             = aws_iam_role.pre_token_generation.arn
  handler          = "index.handler"
  runtime          = "nodejs22.x"
  architectures    = ["x86_64"]
  timeout          = 5
  memory_size      = 128

  tags = {
    Environment = var.environment
    Name        = "${local.resource_prefix}-pre-token-generation"
  }

  depends_on = [
    aws_iam_role_policy_attachment.pre_token_generation_logs,
    aws_cloudwatch_log_group.pre_token_generation,
  ]
}

resource "aws_lambda_permission" "allow_cognito_pre_token_generation" {
  statement_id  = "AllowCognitoPreTokenGeneration"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.pre_token_generation.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.this.arn
}
