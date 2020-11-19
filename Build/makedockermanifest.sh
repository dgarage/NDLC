#!/bin/bash

source /source/Build/utils.sh

DOCKER_TAG="$DOCKERHUB_USER/$DOCKERHUB_REPO:$VERSION"

echo "Docker tag: $DOCKER_TAG"

sudo mkdir $HOME/.docker
sudo sh -c 'echo "{ \"experimental\": \"enabled\" }" >> $HOME/.docker/config.json'
#
sudo docker login --username=$DOCKERHUB_USER --password=$DOCKERHUB_PASS
#

sudo docker manifest create --amend $DOCKER_TAG $DOCKER_TAG-amd64 $DOCKER_TAG-arm32v7 $DOCKER_TAG-arm64v8
sudo docker manifest annotate $DOCKER_TAG $DOCKER_TAG-amd64 --os linux --arch amd64
sudo docker manifest annotate $DOCKER_TAG $DOCKER_TAG-arm32v7 --os linux --arch arm --variant v7
sudo docker manifest annotate $DOCKER_TAG $DOCKER_TAG-arm64v8 --os linux --arch arm64 --variant v8
sudo docker manifest push $DOCKER_TAG -p