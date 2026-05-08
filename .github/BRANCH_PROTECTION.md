# Branch Protection Setup

Configure the following in GitHub -> Settings -> Branches -> Add rule:

## main branch
- Require a pull request before merging: YES
- Require approvals: 1
- Require status checks to pass before merging: YES
  Required checks:
    - Lint & Format Check
    - Build
    - Security Scan
    - Test & Coverage
    - Docker Build
- Require branches to be up to date before merging: YES
- Do not allow bypassing the above settings: YES

## develop branch
- Require status checks to pass:
    - Lint & Format Check
    - Build
    - Test & Coverage
