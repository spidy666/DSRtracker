🇩🇪 [Deutsch](SECURITY.md) | 🇬🇧 **English**

> This is a translation of the German security policy. If the two differ, the German version applies.

# Security Policy

## Supported versions

Security fixes are only provided for the **latest version** on the [release page](https://github.com/spidy666/DSRtracker/releases/latest). Before reporting, please check whether the problem still exists there.

## Reporting a vulnerability

Please do **not open a public issue** while the vulnerability is not fixed.

Instead, report it confidentially via GitHub:
**Security → Report a vulnerability** ([directly to the form](https://github.com/spidy666/DSRtracker/security/advisories/new)).

Helpful information:
- affected version and Windows version
- what exactly happens and how to reproduce it
- what impact you expect (e.g. access from outside, data loss)

I will confirm receipt as soon as possible and get back to you once there is an assessment or a fix. This is a hobby project by a single person, so please be patient. Reports in English or German are both fine.

## What counts as a security problem

For example:
- The local web server can be reached or controlled from another computer or from a foreign website.
- The tracker writes to game memory, modifies game files or accesses other processes.
- Files outside the data folder (`%APPDATA%\DSRTracker`) are read or written.
- The release exe differs from the source code (checksum or provenance attestation do not match).

How the tracker is secured is described in the [README](README.en.md#security).
