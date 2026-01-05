import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildFeatures.perfmon

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

    buildType(LocalDeploy)

    subProject(GCP)
}

object LocalDeploy : BuildType({
    name = "LocalDeploy"
    description = "Deploys locally using the `docker-compose.yml` from the solution root."

    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    features {
        perfmon {
        }
    }
})


object GCP : Project({
    name = "GCP"
    description = "GCP related build and deployment configurations. "

    buildType(GCP_Deploy)
})

object GCP_Deploy : BuildType({
    name = "Deploy"

    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    features {
        perfmon {
        }
    }
})
