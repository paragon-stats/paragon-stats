# GitHub rulesets

`main-ruleset.json` is the committed copy of the `main-protection` ruleset (id 18196476).

## Apply

Run after the change merges and is pulled:

```text
gh api -X PUT repos/paragon-stats/paragon-stats/rulesets/18196476 --input docs/ci/github-rulesets/main-ruleset.json
```

## Code scanning gate

The `code_scanning` rule blocks a pull request that introduces a CodeQL alert of any severity.

Established on a sibling repository on 2026-11-10 (GitHub's docs do not state it):

- GitHub accepts the rule on the Free plan for a public repository.
- It judges only the alerts a pull request introduces, not the open alerts on `main`. A pull request
  with none of its own showed code scanning as satisfied while `main` had 414 open.

A pull request now waits for CodeQL and must fix or dismiss what it introduces.

### Dismiss a deliberate alert

Dismiss it in the pull request's code scanning alert, or with the API, always with a reason:

```text
gh api -X PATCH repos/paragon-stats/paragon-stats/code-scanning/alerts/<number> -f state=dismissed -f dismissed_reason="false positive" -f dismissed_comment="<evidence>"
```

`dismissed_reason` is one of `false positive`, `won't fix` or `used in tests`.

### Remove the rule

Delete the `code_scanning` entry from `main-ruleset.json` and apply the file again.
