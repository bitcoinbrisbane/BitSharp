del pull-tests.jar
rmdir -force -recurse bin

if (Test-Path bitcoinj)
{
	git --git-dir=bitcoinj/.git pull
}
else
{
	git clone https://github.com/bitcoinj/bitcoinj.git bitcoinj
}

mvn -f bitcoinj/pom.xml -DskipTests clean package
cp -force bitcoinj/core/target/pull-tests.jar .
