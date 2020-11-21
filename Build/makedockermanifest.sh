#!/bin/bash

source ./Build/utils.sh

DOCKER_TAG="$DOCKERHUB_USER/$DOCKERHUB_REPO:$VERSION"
DOCKER_TAG_LATEST="$DOCKERHUB_USER/$DOCKERHUB_REPO"

echo "Docker tag: $DOCKER_TAG"

sudo mkdir -p $HOME/.docker
sudo sh -c 'echo "{ \"experimental\": \"enabled\" }" > $HOME/.docker/config.json'
#
sudo docker login "--username=$DOCKERHUB_USER" "--password=$DOCKER_API_KEY"
#

for target in "$DOCKER_TAG" "$DOCKER_TAG_LATEST"; do
	sudo docker manifest create --amend $target $DOCKER_TAG-amd64 $DOCKER_TAG-arm32v7 $DOCKER_TAG-arm64v8
	sudo docker manifest annotate $target $DOCKER_TAG-amd64 --os linux --arch amd64
	sudo docker manifest annotate $target $DOCKER_TAG-arm32v7 --os linux --arch arm --variant v7
	sudo docker manifest annotate $target $DOCKER_TAG-arm64v8 --os linux --arch arm64 --variant v8
	sudo docker manifest push $target -p
done