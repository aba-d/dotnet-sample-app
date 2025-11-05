#!/bin/bash
set -e  # exit if any command fails

# Common configuration
ORG_KEY="org-testfordemo"
SONAR_HOST="https://sonarcloud.io"
SONAR_TOKEN="bb79ddc2d2ede4c5adbfb9ba148b18faf404f222"
PROJECT_KEY="org-testfordemo_dotnet-sample-app"

# Detect project type
detect_project_type() {
    if [ -f "pom.xml" ]; then
        echo "java-maven"
    elif [ -f "build.gradle" ] || [ -f "build.gradle.kts" ]; then
        echo "java-gradle"
    elif find . -maxdepth 2 -type f -name "*.sln" -o -name "*.csproj" | grep -q .; then
        echo "dotnet"
    elif [ -f "package.json" ]; then
        echo "nodejs"
    elif [ -f "requirements.txt" ] || [ -f "setup.py" ]; then
        echo "python"
    else
        echo "unknown"
    fi
}

# Function to analyze Java Maven project
analyze_java_maven() {
    echo "🚀 Starting SonarScanner analysis for Java Maven project..."
    
    # Download SonarScanner for Maven if not present
    mvn --version || {
        echo "Maven not found. Please install Maven first."
        exit 1
    }

    # Run Maven build with Sonar analysis
    mvn clean verify sonar:sonar \
        -Dsonar.projectKey="$PROJECT_KEY" \
        -Dsonar.organization="$ORG_KEY" \
        -Dsonar.host.url="$SONAR_HOST" \
        -Dsonar.login="$SONAR_TOKEN" \
        -Dsonar.coverage.jacoco.xmlReportPaths=target/site/jacoco/jacoco.xml
}

# Function to analyze Java Gradle project
analyze_java_gradle() {
    echo "🚀 Starting SonarScanner analysis for Java Gradle project..."
    
    # Run Gradle build with Sonar analysis
    ./gradlew sonarqube \
        -Dsonar.projectKey="$PROJECT_KEY" \
        -Dsonar.organization="$ORG_KEY" \
        -Dsonar.host.url="$SONAR_HOST" \
        -Dsonar.login="$SONAR_TOKEN" \
        -Dsonar.coverage.jacoco.xmlReportPaths=build/reports/jacoco/test/jacocoTestReport.xml
}

# Function to analyze .NET project
analyze_dotnet() {
    echo "🚀 Starting SonarScanner analysis for .NET project..."
    
    # Find test project
    TEST_PROJECT_PATH=$(find . -name "*Tests.csproj" -o -name "*Test.csproj" | head -n 1)
    
    # Restore local tools
    dotnet tool restore

    # Start Sonar analysis
    dotnet tool run dotnet-sonarscanner begin \
        /o:"$ORG_KEY" \
        /k:"$PROJECT_KEY" \
        /d:sonar.host.url="$SONAR_HOST" \
        /d:sonar.token="$SONAR_TOKEN" \
        /d:sonar.cs.vstest.reportsPaths="**/*.trx" \
        /d:sonar.cs.cobertura.reportsPaths="**/coverage.cobertura.xml"

    # Build and test
    dotnet build --no-incremental
    if [ -n "$TEST_PROJECT_PATH" ]; then
        dotnet test "$TEST_PROJECT_PATH" \
            --collect:"XPlat Code Coverage" \
            --logger:"trx;LogFileName=test-results.trx" \
            --results-directory "./coverage"
    fi

    # End Sonar analysis
    dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
}

# Function to analyze Node.js project
analyze_nodejs() {
    echo "🚀 Starting SonarScanner analysis for Node.js project..."
    
    # Install dependencies if needed
    npm install

    # Install sonarqube-scanner if not already installed
    npm install --save-dev sonarqube-scanner

    # Run tests with coverage
    npm run test -- --coverage

    # Run Sonar analysis
    node_modules/.bin/sonar-scanner \
        -Dsonar.projectKey="$PROJECT_KEY" \
        -Dsonar.organization="$ORG_KEY" \
        -Dsonar.sources=. \
        -Dsonar.host.url="$SONAR_HOST" \
        -Dsonar.login="$SONAR_TOKEN" \
        -Dsonar.javascript.lcov.reportPaths=coverage/lcov.info
}

# Function to analyze Python project
analyze_python() {
    echo "🚀 Starting SonarScanner analysis for Python project..."
    
    # Create and activate virtual environment
    python -m venv venv
    source venv/bin/activate

    # Install dependencies
    pip install -r requirements.txt
    pip install coverage pytest pytest-cov

    # Run tests with coverage
    python -m pytest --cov=. --cov-report=xml

    # Download and extract sonar-scanner
    if [ ! -d "sonar-scanner" ]; then
        wget https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-4.7.0.2747.zip
        unzip sonar-scanner-cli-4.7.0.2747.zip
        mv sonar-scanner-4.7.0.2747 sonar-scanner
    fi

    # Run Sonar analysis
    ./sonar-scanner/bin/sonar-scanner \
        -Dsonar.projectKey="$PROJECT_KEY" \
        -Dsonar.organization="$ORG_KEY" \
        -Dsonar.sources=. \
        -Dsonar.host.url="$SONAR_HOST" \
        -Dsonar.login="$SONAR_TOKEN" \
        -Dsonar.python.coverage.reportPaths=coverage.xml

    # Deactivate virtual environment
    deactivate
}

# Main execution
PROJECT_TYPE=$(detect_project_type)
echo "📦 Detected project type: $PROJECT_TYPE"

case $PROJECT_TYPE in
    "java-maven")
        analyze_java_maven
        ;;
    "java-gradle")
        analyze_java_gradle
        ;;
    "dotnet")
        analyze_dotnet
        ;;
    "nodejs")
        analyze_nodejs
        ;;
    "python")
        analyze_python
        ;;
    *)
        echo "❌ Unknown project type. Please configure manually."
        exit 1
        ;;
esac

# Function to show code coverage summary
show_coverage_summary() {
    # 5️⃣ Show code coverage summary
    echo "============== Code Coverage Summary ================"
    case $1 in
        "dotnet")
            COVERAGE_FILE=$(find . -type f -name "coverage.cobertura.xml" | head -n 1)
            if [ -f "$COVERAGE_FILE" ]; then
                echo "� .NET Code Coverage Summary:"
                # Install reportgenerator if needed (with specific version for consistency)
                dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.1.22 --verbosity quiet || true
                reportgenerator -reports:"$COVERAGE_FILE" -targetdir:coverage-summary -reporttypes:TextSummary
                cat coverage-summary/Summary.txt
            else
                echo "⚠️ No coverage report found. Looking in: $(pwd)"
                find . -type f -name "coverage*.xml" -ls
            fi
            ;;
        "java-maven" | "java-gradle")
            COVERAGE_FILE=$(find . -name "jacoco.xml" -o -name "jacocoTestReport.xml" | head -n 1)
            if [ -n "$COVERAGE_FILE" ]; then
                echo "📊 Line Coverage:"
                grep -A 1 "<report" "$COVERAGE_FILE" | grep "line-coverage" | cut -d'"' -f2 | awk '{print int($1 * 100)"%"}'
                echo "📊 Branch Coverage:"
                grep -A 1 "<report" "$COVERAGE_FILE" | grep "branch-coverage" | cut -d'"' -f2 | awk '{print int($1 * 100)"%"}'
            else
                echo "❌ No coverage report found"
            fi
            ;;
        "nodejs")
            if [ -f "coverage/lcov-report/index.html" ]; then
                echo "📊 Coverage Summary:"
                grep -A 5 "<div class='fl pad1y space-right2'>" coverage/lcov-report/index.html | \
                grep "Statements\|Branches\|Functions\|Lines" | \
                sed -E 's/.*>([^<]+)<.*/\1/' | \
                sed 's/^/    /'
            else
                echo "❌ No coverage report found"
            fi
            ;;
        "python")
            if [ -f "coverage.xml" ]; then
                echo "📊 Coverage Summary:"
                grep -A 1 "<coverage" coverage.xml | grep "line-rate\|branch-rate" | \
                awk -F'"' '{print $1 ~ /line/ ? "Line Coverage: " : "Branch Coverage: "; print int($2 * 100)"%"}'
            else
                echo "❌ No coverage report found"
            fi
            ;;
        *)
            echo "⚠️ Coverage reporting not supported for this project type"
            ;;
    esac
    echo "=================================================="
}

# Show coverage summary for the current project
show_coverage_summary "$PROJECT_TYPE"

# Show quality gate status
echo "================== SonarCloud Quality Gate =================="
STATUS_JSON=$(curl -s -u "$SONAR_TOKEN:" "$SONAR_HOST/api/qualitygates/project_status?projectKey=$PROJECT_KEY")
GATE_STATUS=$(echo "$STATUS_JSON" | jq -r '.projectStatus.status')
FAILED_CONDITIONS=$(echo "$STATUS_JSON" | jq -r '.projectStatus.conditions[] | select(.status=="ERROR") | "\(.metricKey): \(.actualValue) (expected: \(.errorThreshold))"')

echo "Project: $PROJECT_KEY"
echo "Status: $GATE_STATUS"
if [[ -n "$FAILED_CONDITIONS" ]]; then
    echo "❌ Failed conditions:"
    echo "$FAILED_CONDITIONS"
else
    echo "✅ All conditions passed!"
fi
echo "=============================================================="