#!groovy

def PowerShell(psCmd) {
    bat "powershell.exe -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"$psCmd; EXIT \$global:LastExitCode\""
}

stage("tests") {

        
            node('nugetci-e2e-01') {
                ws("w\\${env.BRANCH_NAME.replaceAll('/', '-')}") {
                    checkout scm
                    PowerShell(". '.\\configure.ps1' -ci -v")
                    PowerShell(". '.\\build.ps1' -ci -v -ea Stop")
                }
            }
}