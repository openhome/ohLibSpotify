# Defines the build behaviour for continuous integration builds.

import sys

try:
    from ci import (OpenHomeBuilder, require_version)
except ImportError:
    print "You need to update ohDevTools."
    sys.exit(1)

require_version(19)


class Builder(OpenHomeBuilder):
    test_location = 'build/{assembly}/bin/{configuration}/{assembly}.dll'
    def setup(self):
        self.set_nunit_location('dependencies/nuget/NUnit.Runners.2.6.1/tools/nunit-console-x86.exe')

    def clean(self):
        self.msbuild('src/INSERT_PROJECT_NAME.sln', target='Clean', configuration=self.configuration)

    def build(self):
        self.msbuild('src/INSERT_PROJECT_NAME.sln', target='Build', configuration=self.configuration)

    def test(self):
        pass
        #self.nunit('ohOs.Tests')

    def publish(self):
        if self.options.auto and not self.platform == 'Linux-x86':
            # Only publish from one CI platform, Linux-x86.
            return
        self.publish_package(
                'PACKAGENAME-AnyPlatform-{configuration}.tar.gz',
                'PACKAGENAME/PACKAGENAME-{version}-AnyPlatform-{configuration}.tar.gz')
