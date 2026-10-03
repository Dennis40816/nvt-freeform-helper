function Get-PrivatePathPatterns {
    # Match workstation profiles across Windows, Unix, Git Bash, and WSL without flagging placeholders.
    return @(
        '(?i)(?<![a-z0-9_])[a-z]:[\\/]+users[\\/]+(?!(?:public|runneradmin)(?![\p{L}\p{N}_-]|\.[\p{L}\p{N}_]))[\p{L}\p{N}_][^\\/\s"''<>|:*?]*',
        '(?i)(?:(?<![a-z0-9_])[a-z]:[\\/]+|(?<![a-z0-9_.])/(?:mnt/|cygdrive/)?[a-z]/)dennis(?![a-z])',
        '(?i)(?<![a-z0-9_.])/(?:(?:mnt/|cygdrive/)?[a-z]/)?(?:users|home)/(?!(?:runner|runneradmin|public)(?![\p{L}\p{N}_-]|\.[\p{L}\p{N}_]))[\p{L}\p{N}_][^\\/\s"''<>|:*?]*'
    )
}
