# Protected notes

[Español](PROTECTED_NOTES.es.md)

A protected note is encrypted with AES-GCM using a key derived from its password with PBKDF2
SHA-256. Aldune does not store the password or a usable copy of it.

While it is locked, the dock and Manage notes show only `Protected note`, unless you give it a **visible name**
(optional, per note: when protecting it or from “Visible name…”). That name is a separate field, in the clear, a single line and up to 60
characters; it is not taken from the content and anyone who sees your screen sees it. It travels through sync (the `protectedTitle` field of the
envelope, encrypted with the profile key like tags), without raising format 4: Windows versions before 1.8.0 and
Android versions before 0.2.0 ignore it and lose it if they resend that note, so it is best to update all devices. Removing the
protection deletes it. When you open the note
the password is asked for; it stays unlocked during that editing session and is locked again
when you close it.

Protection is part of the sync envelope: the server and the shared folder only
receive encrypted data, and each device must enter the password to read the note. Versions
older than sync format 3 reject the envelope so as not to silently replace a protected
note.

The password cannot be recovered. It is a good idea to try with a test note first and keep the
password in a password manager.
