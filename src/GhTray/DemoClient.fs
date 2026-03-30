namespace GhTray

type DemoClient() =
    let makePr number repo title isDraft checkStatus reviewStatus hasConflicts =
        { Title = title
          Url = $"https://github.com/%s{repo}/pull/%d{number}"
          Number = number
          Repository = repo
          IsDraft = isDraft
          CheckStatus = checkStatus
          ReviewStatus = reviewStatus
          HasConflicts = hasConflicts }

    interface IGitHubClient with
        member _.GetUsername() = async { return "demo-user" }

        member _.FetchPullRequests(_username: string) = async {
            return
                { Mine =
                    [ makePr 1 "demo/app" "Add user authentication" false (Some Success) (Some Approved) false
                      makePr 2 "demo/app" "WIP: Refactor database layer" true None None false
                      makePr 3 "demo/api" "Fix pagination endpoint" false (Some Failure) None false
                      makePr 4 "demo/api" "Update dependencies" false None None true
                      makePr 5 "demo/web" "Add dark mode support" false (Some Pending) None false ]
                  ReviewRequested =
                    [ makePr 10 "demo/lib" "Improve error handling" false None (Some ChangesRequested) false
                      makePr 11 "demo/lib" "Add retry logic" false (Some Success) None false
                      makePr 12 "demo/docs" "Update API documentation" false None None false ]
                  Involved =
                    [ makePr
                          20
                          "demo/infra"
                          "Migrate to new CI pipeline"
                          false
                          (Some Failure)
                          (Some ChangesRequested)
                          false
                      makePr 21 "demo/infra" "Add monitoring dashboard" true (Some Pending) None false ] }
        }
