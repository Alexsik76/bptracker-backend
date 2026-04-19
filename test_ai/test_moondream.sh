#!/bin/bash

IMAGE_PATH=$1
PROMPT_FILE="prompt.txt"
API_URL="http://moondream.lan:11434/api/chat"

# Перевірка наявності зображення
if [ ! -f "$IMAGE_PATH" ]; then
    echo "Error: Image file '$IMAGE_PATH' not found!"
    exit 1
fi

# Перевірка наявності файлу з промптом
if [ ! -f "$PROMPT_FILE" ]; then
    echo "Error: '$PROMPT_FILE' not found! Create it and put your question there."
    exit 1
fi

# Зчитуємо проンプт у змінну
PROMPT_TEXT=$(cat "$PROMPT_FILE")

echo "--- Current Prompt ---"
echo "$PROMPT_TEXT"
echo "----------------------"

echo "Encoding image..."
base64 -w 0 "$IMAGE_PATH" > /tmp/img_base64.txt

echo "Building JSON payload..."
jq -n \
  --arg model "gemma4:e4b" \
  --arg prompt "$PROMPT_TEXT" \
  --rawfile img /tmp/img_base64.txt \
  '{
    model: $model,
    messages: [
      {
        role: "user",
        content: $prompt,
        images: [$img]
      }
    ],
    stream: false,
    format: "json",
      options: {
      temperature: 0.2,
      seed: 42,
      num_predict: 50,
      num_ctx: 2048
    }
  }' > payload.json
echo "Sending request to $API_URL..."
# Додано jq . для гарного форматування відповіді
curl -s -X POST "$API_URL" -H "Content-Type: application/json" -d @payload.json | jq .

# Cleanup
rm /tmp/img_base64.txt
