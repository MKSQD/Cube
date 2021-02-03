namespace Cube.Replication {
    public static class Constants {
        public const float ClientReplicaInactiveDestructionTimeSec = 2.2f;
        public const float ServerReplicaIdRecycleTime = ClientReplicaInactiveDestructionTimeSec * 1.2f;
    }
}
