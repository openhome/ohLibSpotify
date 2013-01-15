# Defines the build behaviour for continuous integration builds.

import sys

try:
    from ci import (OpenHomeBuilder, require_version)
except ImportError:
    print "You need to update ohDevTools."
    sys.exit(1)

require_version(22)


class Builder(OpenHomeBuilder):
    test_location = 'build/{assembly}/bin/{configuration}/{assembly}.dll'
    def setup(self):
        self.set_nunit_location('dependencies/nuget/NUnit.Runners.2.6.1/tools/nunit-console-x86.exe')

    def clean(self):
        self.msbuild('src/SpotifySharp.sln', target='Clean', configuration=self.configuration)

    def build(self):
        self.msbuild('src/SpotifySharp.sln', target='Build', configuration=self.configuration)

    def test(self):
        pass
        #self.nunit('ohOs.Tests')

    def publish(self):
        self.publish_package(
                'ohLibSpotify-{platform}-{configuration}.tar.gz',
                'ohLibSpotify/ohLibSpotify-{version}-AnyPlatform-{configuration}.tar.gz')
