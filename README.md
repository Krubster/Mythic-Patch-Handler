# Mythic Patch Handler
This project is an attempt to provide a better, open source, alternative to the Mythic Patch Handler (MYPHandler.dll) used in the official Return of Reckoning Launcher.

This also is used as the MYPHandler found in [Return of Reckoning Launcher](https://github.com/ThiconZ/Return-of-Reckoning-Launcher)

It features the following over the current official one as of Apr 9 2017:

* No longer requires Performance Counters
    * If Performance Counters are unable to be found it will not use them, preventing the errors seen in previous versions
    * This means no more errors when running in WINE on Linux systems
