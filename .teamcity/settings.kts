import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildFeatures.perfmon
import jetbrains.buildServer.configs.kotlin.buildSteps.DockerCommandStep
import jetbrains.buildServer.configs.kotlin.buildSteps.dockerCommand
import jetbrains.buildServer.configs.kotlin.buildSteps.dotnetTest
import jetbrains.buildServer.configs.kotlin.buildSteps.script

/*
The settings script is an entry point for defining a TeamCity
project hierarchy. The script should contain a single call to the
project() function with a Project instance or an init function as
an argument.

VcsRoots, BuildTypes, Templates, and subprojects can be
registered inside the project using the vcsRoot(), buildType(),
template(), and subProject() methods respectively.

To debug settings scripts in command-line, run the

    mvnDebug org.jetbrains.teamcity:teamcity-configs-maven-plugin:generate

command and attach your debugger to the port 8000.

To debug in IntelliJ Idea, open the 'Maven Projects' tool window (View
-> Tool Windows -> Maven Projects), find the generate task node
(Plugins -> teamcity-configs -> teamcity-configs:generate), the
'Debug' option is available in the context menu for the task.
*/

version = "2025.11"

project {
    description = "Test CI/CD configuration for an ASP.NET web application to verify certain pipeline characteristics such as deployment configurations (ex. Dockerized containers vs. raw VMs)."

    buildType(LocalDeploy)
    buildType(GcpArtifactDeploy)
}

object GcpArtifactDeploy : BuildType({
    name = "GCP Artifact Deploy"
    description = "Publishes, configures, and packages as artifacts, which are then uploaded to GCP Compute Engine instances to be ran."

    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    features {
        perfmon {
        }
    }
})

object LocalDeploy : BuildType({
    name = "GCP Docker Deploy"
    description = "Builds the Docker images foreach part of the web application, then deploying those built images to GCP Cloud Run instances."

    enablePersonalBuilds = false
    type = BuildTypeSettings.Type.DEPLOYMENT
    buildNumberPattern = "%MAJOR_VERSION%.%MINOR_VERSION%.%build.counter%"
    maxRunningBuilds = 1

    params {
        param("backend.jwt_secret", "ThisIsAVeryLongSecretKeyForJWTTokenGeneration123456789")
        text("dockerhub.username", "chronoalpha", description = "The user used when re-tagging an image. Assumed to be the same as with what the Docker CLI is currently authenticated with.", readOnly = true, allowEmpty = true)
        param("MINOR_VERSION", "0")
        param("backend.connection_string", " ")
        param("gcp.service_account", "personal-sa@stellar-aleph-483607-k2.iam.gserviceaccount.com.json")
        param("MAJOR_VERSION", "0")
    }

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        dotnetTest {
            name = "Test"
            id = "Test"
            enabled = false
            projects = "tests/WebApi.Tests/WebApi.Tests.csproj tests/WebFrontend.Tests.E2E/WebFrontend.Tests.E2E.csproj tests/WebFrontend.Tests/WebFrontend.Tests.csproj"
            coverage = dotcover {
            }
        }
        dockerCommand {
            name = "Docker Build API"
            id = "DockerCommand"
            commandType = build {
                source = file {
                    path = "src/WebApi/Dockerfile"
                }
                contextDir = "."
                platform = DockerCommandStep.ImagePlatform.Linux
                namesAndTags = "dotnet-teamcity-template-api"
                commandArgs = "--pull"
            }
        }
        dockerCommand {
            name = "Docker Build Frontend"
            id = "DockerCommand_1"
            commandType = build {
                source = file {
                    path = "src/WebFrontend/Dockerfile"
                }
                contextDir = "."
                platform = DockerCommandStep.ImagePlatform.Linux
                namesAndTags = "dotnet-teamcity-template-frontend"
                commandArgs = "--pull"
            }
        }
        script {
            name = "DockerHub Push"
            id = "DockerHub_Push_API"
            workingDir = "."
            scriptContent = """
                VERSION="%MAJOR_VERSION%.%MINOR_VERSION%.%build.counter%"
                BACKEND_TAG="%dockerhub.username%/dotnet-teamcity-template-api:${'$'}VERSION"
                FRONTEND_TAG="%dockerhub.username%/dotnet-teamcity-template-frontend:${'$'}VERSION"
                
                echo "VERSION: ${'$'}VERSION"
                echo "BACKEND_TAG: ${'$'}BACKEND_TAG"
                echo "FRONTEND_TAG: ${'$'}FRONTEND_TAG"
                
                docker tag dotnet-teamcity-template-api:latest ${'$'}BACKEND_TAG
                docker tag dotnet-teamcity-template-frontend:latest ${'$'}FRONTEND_TAG
                
                docker push ${'$'}BACKEND_TAG
                docker push ${'$'}FRONTEND_TAG
            """.trimIndent()
        }
        script {
            name = "Terraform Apply"
            id = "Terraform_Apply"
            scriptContent = """
                cd ./tf/cloudrun
                ls -la
                
                # init gcloud cli
                CREDENTIALS_FILE="/opt/gcp/service_accounts/%gcp.service_account%"
                export GOOGLE_APPLICATION_CREDENTIALS="${'$'}CREDENTIALS_FILE"
                gcloud auth activate-service-account --key-file=${'$'}CREDENTIALS_FILE
                gcloud auth list
                
                # init vars
                VERSION="%MAJOR_VERSION%.%MINOR_VERSION%.%build.counter%"
                BACKEND_TAG="%dockerhub.username%/dotnet-teamcity-template-api:${'$'}VERSION"
                FRONTEND_TAG="%dockerhub.username%/dotnet-teamcity-template-frontend:${'$'}VERSION"
                
                echo "VERSION: ${'$'}VERSION"
                echo "BACKEND_TAG: ${'$'}BACKEND_TAG"
                echo "FRONTEND_TAG: ${'$'}FRONTEND_TAG"
                
                export TF_VAR_project_name="personal"
                export TF_VAR_gcp_project_id="stellar-aleph-483607-k2"
                export TF_VAR_gcp_region="northamerica-northeast1"
                
                export TF_VAR_webapi_image="${'$'}BACKEND_TAG"
                export TF_VAR_webfrontend_image="${'$'}FRONTEND_TAG"
                
                export TF_VAR_environment="prod"
                export TF_VAR_database_connection_string="%backend.connection_string%"
                export TF_VAR_jwt_secret="%backend.jwt_secret%"
                export TF_VAR_cors_allowed_origins=""
                
                # remove state files just incase they are there
                rm -rf .terraform .terraform.lock.hcl terraform.tfstate terraform.tfstate.backup cloud_run
                
                # echo "yes" | terraform init -backend-config="bucket=stellar-aleph-483607-k2-terraform-state"
                echo "yes" | terraform init -migrate-state -backend-config="bucket=stellar-aleph-483607-k2-terraform-state"
                
                terraform plan -destroy -out=cloud_run
                terraform apply -auto-approve cloud_run
                terraform plan -out=cloud_run
                terraform apply -auto-approve cloud_run
                
                # terraform apply -destroy -auto-approve -target="google_cloud_run_service.webfrontend" -target="google_cloud_run_service.webapi_public"
                # terraform plan -out=cloud_run -target="google_cloud_run_service.webfrontend" -target="google_cloud_run_service.webapi_public"
                # terraform apply "cloud_run"
            """.trimIndent()
        }
    }

    features {
        perfmon {
        }
    }
})
