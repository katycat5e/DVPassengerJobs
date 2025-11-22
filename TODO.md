# TODO



- [x] Rebalancing of licenses (cost, Multipliers, …)
    - [x] Bonus time for regional trains is currently equal to freight hauls since the rural stations are ignored in the distance calculation
    - [x] Passengers 1 should depend on fragile
    - [x] Pricing, insurance copay and time decrease of licenses
- [x] Find out how to add a new asset, i.e. the license2 Image
- [x] Require license1 to buy license2
- [ ] Implement proper migrations to keep backwards compatibility
- [x] Both licenses Need different descriptions which need to be added in the lanugages csv file
- [x] Correctly assign licenses for Jobs, i.e. regional->license1 and express->license2
- [x] Refactor the current Interface->Class->2 Instances model for the License data, maybe a record works?
- [x] Jobs with >5 coaches should require long 1 license
- [ ] Cleanup code changes, i.e. if/else vs. ternary operators, if possible use AI code review
- [ ] Testing
    - [ ] Backwards compatibility: create save with upstream mod Version and buy license1, than load save with new mod Version
    - [x] Try to buy license2 without owning license1 (should not work)
    - [x] Try accepting Jobs with/without the correct licenses
    - [ ] Get a feeling for the new balancing, regional jobs Need to feel much better in Terms of payment and time limit
