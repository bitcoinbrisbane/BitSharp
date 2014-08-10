if (Test-Path pull-tests.jar) { del -force pull-tests.jar }
if (Test-Path bin) { rmdir -force -recurse bin }

if (Test-Path bitcoinj)
{
	pushd bitcoinj
	git pull
	popd
}
else
{
	git clone https://github.com/pmlyon/bitcoinj.git bitcoinj
}

mvn -f bitcoinj/pom.xml -DskipTests clean package
cp -force bitcoinj/core/target/pull-tests.jar .
