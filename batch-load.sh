#!/bin/bash

# This script uploads a data file to Amazon S3 using a pre-signed URL. An automated process batch processes the file automatically.
# Usage: ./script_name.sh <dataFile>

# Exit if any command fails
set -e

# Ensure a file name is provided as an argument
if [ $# -ne 1 ]; then
    echo "Usage: $0 <dataFile>"
    exit 1
fi

dataFile="$1"

# Check if the data file exists
if [ ! -f "$dataFile" ]; then
    echo "Error: File not found: $dataFile"
    exit 1
fi

# Check if the solution has been deployed
if [ ! -f "cdk.outputs.json" ]; then
    echo "Error: Please make sure that the solution has been successfully deployed and then retry."
    exit 1
fi

echo "Starting batch loading process for: $dataFile"

# Extract the application endpoint URL from configuration
applicationEndpoint=$(jq -r '.["JPMC-OrderManagement-NetworkingStack"]["ApplicationApiEndpointHttpUrl"]' cdk.outputs.json)

if [ -z "$applicationEndpoint" ]; then
    echo "Error: Application endpoint is not set."
    exit 1
fi

echo "Retrieving the pre-signed URL for file upload ..."
# Request a pre-signed URL from the application API
presignedUrl=$(curl -s -X 'POST' "$applicationEndpoint/orders/batch-load")
presignedUrl=$(echo "$presignedUrl" | jq -r .preSignedUrl)

if [ -z "$presignedUrl" ]; then
    echo "Failed to obtain pre-signed URL."
    exit 1
fi

echo "Uploading $dataFile to S3 ..."
# Upload the file to S3 using the pre-signed URL
curl -X PUT -T "$dataFile" "$presignedUrl"
status=$?

# Check the status of the upload
if [ $status -eq 0 ]; then
    echo "The data file has been successfully uploaded to Amazon S3 and will be batch-loaded shortly."
else
    echo "The data file upload process has failed with status: $status. Please contact an administrator if the issue persists."
    exit $status
fi
