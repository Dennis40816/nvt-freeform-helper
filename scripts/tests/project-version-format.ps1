function Get-ProjectVersionPattern {
    # One line with MAJOR.MINOR.PATCH, an optional development suffix -g.N, and a CRLF ending. No leading zeros.
    # Match it with -cmatch so that the suffix letter stays lower case.
    return '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-g\.[1-9][0-9]*)?\r\n\z'
}
