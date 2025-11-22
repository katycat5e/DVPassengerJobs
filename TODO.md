# TODO



- [ ] Rebalancing of licenses (cost, Multipliers, …)
    - [ ] Bonus time for regional trains is currently equal to freight hauls. Should be cummulated distance of each station + a bonus for each stop
    - [ ] Express bonus time is way too long, e.g. 280min for Harbor to GF via CS,CW
- [ ] Find out how to add a new asset, i.e. the license2 Image
- [x] Require license1 to buy license2
- [ ] Rename license1 to License to hopefully Keep backwards compatibility
- [x] Both licenses Need different descriptions which need to be added in the lanugages csv file
- [x] Correctly assign licenses for Jobs, i.e. regional->license1 and express->license2
- [ ] Refactor the current Interface->Class->2 Instances model for the License data, maybe a record works?
- [ ] Jobs with >6 coaches should require long 1 license
- [ ] Cleanup code changes, i.e. if/else vs. ternary operators, if possible use AI code review
- [ ] Testing
    - [ ] Backwards compatibility: create save with upstream mod Version and buy license1, than load save with new mod Version
    - [ ] Try to buy license2 without owning license1 (should not work)
    - [ ] Try accepting Jobs with/without the correct licenses
    - [ ] Get a feeling for the new balancing, regional jobs Need to feel much better in Terms of payment and time limit
