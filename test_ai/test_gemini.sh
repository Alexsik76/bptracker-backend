#!/bin/bash

# --- CONFIGURATION ---
API_KEY=""
MODEL="gemini-3-flash-preview"  # Or gemini-2.0-flash-exp if available
IMAGE_FILE=$1

# Validating input
if [ ! -f "$IMAGE_FILE" ]; then
    echo "Error: File '$IMAGE_FILE' not found."
    exit 1
fi

# Define the structured prompt for consistent JSON
PROMPT="Analyze this blood pressure monitor image. 
Extract: 
1. Systolic (top number)
2. Diastolic (middle number)
3. Pulse (bottom number)
Return ONLY a valid JSON object with keys: 'systolic', 'diastolic', 'pulse'. No markdown, no comments."

# 1. Prepare Data
MIME_TYPE=$(file -b --mime-type "$IMAGE_FILE")
IMAGE_BASE64=$(base64 -w 0 "$IMAGE_FILE")

# 2. Build Payload
PAYLOAD_FILE=$(mktemp)
cat <<EOF > "$PAYLOAD_FILE"
{
  "contents": [{
    "parts": [
      { "text": "$PROMPT" },
      { "inline_data": { "mime_type": "$MIME_TYPE", "data": "$IMAGE_BASE64" } }
    ]
  }],
  "generationConfig": {
    "response_mime_type": "application/json",
    "temperature": 0.1
  }
}
EOF

# 3. Execution
echo "🚀 Scanning image via Gemini API..."
RESPONSE=$(curl -s -X POST "https://generativelanguage.googleapis.com/v1beta/models/${MODEL}:generateContent?key=${API_KEY}" \
    -H 'Content-Type: application/json' \
    -d @"$PAYLOAD_FILE")

# 4. Cleanup & Result Formatting
rm "$PAYLOAD_FILE"

if echo "$RESPONSE" | jq -e '.error' > /dev/null; then
    echo "❌ API Error:"
    echo "$RESPONSE" | jq .error.message
else
    echo "✅ Success! Results:"
    echo "$RESPONSE" | jq -r '.candidates[0].content.parts[0].text' | jq .
fi
