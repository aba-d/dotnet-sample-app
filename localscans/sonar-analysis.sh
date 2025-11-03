#!/bin/bash
set -e  # exit if any command fails

# 🔑 Replace these with your details
ORG_KEY="org-testfordemo"
PROJECT_KEY="org-testfordemo_dotnet-sample-app"
SONAR_HOST="https://sonarcloud.io"
SONAR_TOKEN="bb79ddc2d2ede4c5adbfb9ba148b18faf404f222"
TEST_PROJECT_PATH="src/dotnet-sample-app.Tests/dotnet-sample-app.Tests.csproj"

echo "🚀 Starting SonarScanner analysis..."

# Restore local tools (ensures team consistency)
dotnet tool restore

# Start Sonar analysis
dotnet tool run dotnet-sonarscanner begin \
  /o:"$ORG_KEY" \
  /k:"$PROJECT_KEY" \
  /d:sonar.host.url="$SONAR_HOST" \
  /d:sonar.token="$SONAR_TOKEN"

# Build and test
dotnet build
dotnet test --collect:"XPlat Code Coverage"

# End Sonar analysis
dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"

# Generate report
dotnet tool run reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report"

echo "✅ Sonar analysis complete!"

# 5️⃣ Show code coverage summary locally
COVERAGE_FILE=$(find ./coverage -type f -name "coverage.opencover.xml" | head -n 1)
if [ -f "$COVERAGE_FILE" ]; then
  echo "🔍 .NET Code Coverage Summary:"
  dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.1.22
  reportgenerator -reports:$COVERAGE_FILE -targetdir:coverage-summary -reporttypes:TextSummary
  cat coverage-summary/Summary.txt
else
  echo "⚠️ No coverage report found."
fi

# 6️⃣ Fetch SonarCloud Quality Gate summary
STATUS_JSON=$(curl -s -u "$SONAR_TOKEN:" "$SONAR_HOST/api/qualitygates/project_status?projectKey=$PROJECT_KEY")
GATE_STATUS=$(echo "$STATUS_JSON" | jq -r '.projectStatus.status')
FAILED_CONDITIONS=$(echo "$STATUS_JSON" | jq -r '.projectStatus.conditions[] | select(.status=="ERROR") | "\(.metricKey): \(.actualValue) (expected: \(.errorThreshold))"')

echo "================== SonarCloud Quality Gate =================="
echo "Project: $PROJECT_KEY"
echo "Status: $GATE_STATUS"
if [[ -n "$FAILED_CONDITIONS" ]]; then
  echo "❌ Failed conditions:"
  echo "$FAILED_CONDITIONS"
else
  echo "✅ All conditions passed!"
fi
echo "=============================================================="